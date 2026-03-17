using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using EWMS.DTOs;
using EWMS.Services;
using EWMS.Models;
using Microsoft.AspNetCore.SignalR;
using EWMS.Hubs;
using Microsoft.EntityFrameworkCore;

namespace EWMS.BackgroundServices
{
    public class SalesOrderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<SalesOrderHub> _hubContext;
        private IConnection? _connection;
        private IModel? _channel;

        public SalesOrderWorker(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IHubContext<SalesOrderHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            Console.WriteLine("=== SalesOrderWorker STARTING ===");

            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:UserName"] ?? "admin",
                Password = _configuration["RabbitMQ:Password"] ?? "admin123",
                DispatchConsumersAsync = true // Bật async consumer
            };

            Console.WriteLine($"Connecting to RabbitMQ at {factory.HostName}:{factory.Port}...");
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            Console.WriteLine("RabbitMQ connection established!");

            _channel.QueueDeclare(
                queue: "sales-order-queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Chỉ xử lý 1 message tại 1 thời điểm (tránh race condition)
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($">>> RECEIVED MESSAGE from queue: {message.Substring(0, Math.Min(100, message.Length))}...");

                try
                {
                    var orderMessage = JsonSerializer.Deserialize<SalesOrderMessageDto>(message);
                    if (orderMessage != null)
                    {
                        Console.WriteLine($">>> Processing sales order for user {orderMessage.UserId}");
                        var result = await ProcessSalesOrder(orderMessage);
                        Console.WriteLine($">>> Processing completed. Success: {result.Success}, Message: {result.Message}");

                        // Đợi 500ms để đảm bảo client đã connect và join group
                        Console.WriteLine($">>> Waiting 500ms for client to connect...");
                        await Task.Delay(500);
                        
                        // Gửi kết quả qua SignalR đến ĐÚNG USER (không broadcast all)
                        Console.WriteLine($">>> Sending result to SignalR group: user_{orderMessage.UserId}");
                        Console.WriteLine($">>> Result data: Success={result.Success}, Message={result.Message}, OrderId={result.SalesOrderId}, UserId={result.UserId}");
                        
                        await _hubContext.Clients.Group($"user_{orderMessage.UserId}")
                            .SendAsync("ReceiveSalesOrderResult", result);
                        
                        Console.WriteLine($">>> Result sent ONLY to user_{orderMessage.UserId} group");
                    }

                    // Acknowledge message sau khi xử lý thành công
                    Console.WriteLine($">>> ACKing message...");
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    Console.WriteLine($">>> Message ACKed successfully");
                }
                catch (Exception ex)
                {
                    // Log error và reject message
                    Console.WriteLine($"!!! ERROR processing message: {ex.Message}");
                    Console.WriteLine($"!!! Stack trace: {ex.StackTrace}");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(
                queue: "sales-order-queue",
                autoAck: false, // Manual acknowledge
                consumer: consumer
            );

            Console.WriteLine("=== SalesOrderWorker STARTED and listening to queue ===");

            return Task.CompletedTask;
        }

        private async Task<SalesOrderResultDto> ProcessSalesOrder(SalesOrderMessageDto message)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EWMSDbContext>();

            // Bắt đầu transaction với Serializable isolation level
            using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                // Group duplicate products và merge quantities
                var groupedDetails = message.Details
                    .GroupBy(d => d.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Quantity = g.Sum(d => d.Quantity),
                        UnitPrice = g.First().UnitPrice
                    })
                    .ToList();

                // 1. Kiểm tra tồn kho (Inventory -> Location -> WarehouseId)
                foreach (var detail in groupedDetails)
                {
                    // Tính tổng tồn kho của product trong warehouse này (sum tất cả locations)
                    var totalInventory = await dbContext.Inventories
                        .Include(i => i.Location)
                        .Where(i => i.ProductId == detail.ProductId && 
                                   i.Location.WarehouseId == message.WarehouseId)
                        .SumAsync(i => i.Quantity ?? 0);

                    if (totalInventory < detail.Quantity)
                    {
                        var product = await dbContext.Products.FindAsync(detail.ProductId);
                        return new SalesOrderResultDto
                        {
                            Success = false,
                            Message = $"Không đủ tồn kho cho sản phẩm {product?.ProductName ?? detail.ProductId.ToString()}. " +
                                     $"Tồn kho hiện tại: {totalInventory}, Yêu cầu: {detail.Quantity}",
                            UserId = message.UserId
                        };
                    }
                }

                // 2. Tạo Sales Order
                decimal totalAmount = groupedDetails.Sum(d => d.Quantity * d.UnitPrice);

                var salesOrder = new SalesOrder
                {
                    WarehouseId = message.WarehouseId,
                    CustomerName = message.CustomerName,
                    CustomerPhone = message.CustomerPhone,
                    CustomerAddress = message.CustomerAddress,
                    ExpectedDeliveryDate = message.ExpectedDeliveryDate,
                    CreatedBy = message.UserId,
                    Status = "Pending",
                    TotalAmount = totalAmount,
                    Notes = message.Notes,
                    CreatedAt = DateTime.Now
                };

                dbContext.SalesOrders.Add(salesOrder);
                await dbContext.SaveChangesAsync();

                // 3. Tạo Sales Order Details
                foreach (var detail in groupedDetails)
                {
                    var orderDetail = new SalesOrderDetail
                    {
                        SalesOrderId = salesOrder.SalesOrderId,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        UnitPrice = detail.UnitPrice
                        // KHÔNG set TotalPrice vì nó là computed column
                    };

                    dbContext.SalesOrderDetails.Add(orderDetail);
                }

                await dbContext.SaveChangesAsync();

                // 4. Trừ tồn kho (Reserved inventory) - trừ từng location theo thứ tự
                foreach (var detail in groupedDetails)
                {
                    var remainingQuantity = detail.Quantity;

                    // Lấy danh sách inventory có sản phẩm này trong warehouse, sắp xếp theo quantity giảm dần
                    var inventories = await dbContext.Inventories
                        .Include(i => i.Location)
                        .Where(i => i.ProductId == detail.ProductId && 
                                   i.Location.WarehouseId == message.WarehouseId &&
                                   i.Quantity > 0)
                        .OrderByDescending(i => i.Quantity)
                        .ToListAsync();

                    // Trừ dần từng location cho đến hết số lượng cần
                    foreach (var inventory in inventories)
                    {
                        if (remainingQuantity <= 0) break;

                        var quantityToDeduct = Math.Min(inventory.Quantity ?? 0, remainingQuantity);
                        inventory.Quantity -= quantityToDeduct;
                        inventory.LastUpdated = DateTime.Now;
                        remainingQuantity -= quantityToDeduct;
                    }
                }

                await dbContext.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                return new SalesOrderResultDto
                {
                    Success = true,
                    Message = $"Tạo đơn hàng #{salesOrder.SalesOrderId} thành công!",
                    SalesOrderId = salesOrder.SalesOrderId,
                    UserId = message.UserId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new SalesOrderResultDto
                {
                    Success = false,
                    Message = $"Lỗi khi tạo đơn hàng: {ex.Message}",
                    UserId = message.UserId
                };
            }
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
