using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories;
using EWMS.Repositories.Interfaces;
using EWMS.ViewModels;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class SalesOrderService : ISalesOrderService
    {
        private readonly ISalesOrderRepository _salesOrderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryCheckService _inventoryCheckService;
        private readonly IWarehouseRepository _warehouseRepository;
        private readonly Repositories.Interfaces.IInventoryRepository _inventoryRepository;
        private readonly EWMSDbContext _context;

        public SalesOrderService(
            ISalesOrderRepository salesOrderRepository,
            IProductRepository productRepository,
            IInventoryCheckService inventoryCheckService,
            IWarehouseRepository warehouseRepository,
            Repositories.Interfaces.IInventoryRepository inventoryRepository,
            EWMSDbContext context)
        {
            _salesOrderRepository = salesOrderRepository;
            _productRepository = productRepository;
            _inventoryCheckService = inventoryCheckService;
            _warehouseRepository = warehouseRepository;
            _inventoryRepository = inventoryRepository;
            _context = context;
        }

        public async Task<SalesOrderListViewModel> GetSalesOrdersByWarehouseAsync(int warehouseId)
        {
            var orders = await _salesOrderRepository.GetSalesOrdersByWarehouseAsync(warehouseId);
            var warehouseName = await _warehouseRepository.GetWarehouseNameByIdAsync(warehouseId) ?? "Unknown";

            var viewModel = new SalesOrderListViewModel
            {
                WarehouseId = warehouseId,
                WarehouseName = warehouseName,
                Orders = orders.Select(MapToViewModel).ToList(),
                Page = 1,
                PageSize = orders.Count,
                TotalCount = orders.Count,
                TotalPages = 1
            };

            return viewModel;
        }

        public async Task<SalesOrderListViewModel> GetSalesOrdersAsync(int warehouseId, string? customer, string? status, int page, int pageSize)
        {
            var (orders, totalCount) = await _salesOrderRepository.GetSalesOrdersAsync(warehouseId, customer, null, null, status, page, pageSize);
            var warehouseName = await _warehouseRepository.GetWarehouseNameByIdAsync(warehouseId) ?? "Unknown";

            var viewModel = new SalesOrderListViewModel
            {
                WarehouseId = warehouseId,
                WarehouseName = warehouseName,
                Orders = orders.Select(MapToViewModel).ToList(),
                FilterCustomer = customer ?? string.Empty,
                FilterStatus = status ?? string.Empty,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return viewModel;
        }

        private static SalesOrderViewModel MapToViewModel(SalesOrder o) => new SalesOrderViewModel
        {
            SalesOrderId = o.SalesOrderId,
            OrderNumber = $"SO{o.SalesOrderId:D4}",
            CustomerName = o.CustomerName,
            CustomerPhone = o.CustomerPhone,
            CustomerAddress = o.CustomerAddress,
            ExpectedDeliveryDate = o.ExpectedDeliveryDate,
            TotalAmount = o.TotalAmount,
            Status = o.Status,
            Notes = o.Notes,
            CreatedAt = o.CreatedAt,
            WarehouseName = o.Warehouse.WarehouseName,
            CreatedBy = o.CreatedBy,
            Details = o.SalesOrderDetails.Select(d => new SalesOrderDetailViewModel
            {
                ProductId = d.ProductId,
                ProductName = d.Product.ProductName,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                TotalPrice = d.TotalPrice ?? 0,
                Unit = d.Product.Unit ?? ""
            }).ToList()
        };

        public async Task<SalesOrderViewModel?> GetSalesOrderByIdAsync(int salesOrderId)
        {
            var order = await _salesOrderRepository.GetSalesOrderByIdAsync(salesOrderId);
            if (order == null) return null;

            return new SalesOrderViewModel
            {
                SalesOrderId = order.SalesOrderId,
                OrderNumber = $"SO{order.SalesOrderId:D4}",
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerAddress = order.CustomerAddress,
                ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                WarehouseName = order.Warehouse.WarehouseName,
                CreatedBy = order.CreatedBy,
                Details = order.SalesOrderDetails.Select(d => new SalesOrderDetailViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TotalPrice = d.TotalPrice ?? 0,
                    Unit = d.Product.Unit ?? ""
                }).ToList()
            };
        }

        public async Task<(bool Success, string Message, int? OrderId)> CreateSalesOrderAsync(
            CreateSalesOrderViewModel model,
            int createdByUserId)
        {
            try
            {
                // Group by ProductId to handle duplicate products (merge quantities)
                var groupedDetails = model.Details
                    .GroupBy(d => d.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Quantity = g.Sum(d => d.Quantity),
                        UnitPrice = g.First().UnitPrice // Use first unit price for duplicates
                    })
                    .ToList();

                var productIds = groupedDetails.Select(d => d.ProductId).ToList();

                // ════════════════════════════════════════════════════════════════════════════
                // BẮT ĐẦU TRANSACTION với Read Committed Isolation Level
                // Sử dụng Row-Level Locking để tránh race condition
                // ════════════════════════════════════════════════════════════════════════════
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // ════════════════════════════════════════════════════════════════════════
                    // BƯỚC 1: LOCK các inventory rows của products trong đơn hàng này
                    // Chỉ các requests cùng product mới phải đợi nhau
                    // Requests khác product sẽ chạy song song (parallel)
                    // ════════════════════════════════════════════════════════════════════════
                    var availableStock = await _inventoryRepository
                        .GetAndLockAvailableStockAsync(productIds, model.WarehouseId);

                    // ════════════════════════════════════════════════════════════════════════
                    // BƯỚC 2: Validate stock cho từng product
                    // ════════════════════════════════════════════════════════════════════════
                    foreach (var detail in groupedDetails)
                    {
                        if (!availableStock.TryGetValue(detail.ProductId, out var available))
                        {
                            await transaction.RollbackAsync();
                            return (false, $"Product ID {detail.ProductId} not found in warehouse!", null);
                        }

                        if (available < detail.Quantity)
                        {
                            await transaction.RollbackAsync();
                            
                            var product = await _productRepository
                                .GetProductByIdAsync(detail.ProductId);
                            
                            return (false,
                                $"Insufficient stock for {product?.ProductName ?? "product"}! " +
                                $"Available: {available}, Required: {detail.Quantity}",
                                null);
                        }
                    }

                    // ════════════════════════════════════════════════════════════════════════
                    // BƯỚC 3: Tạo Sales Order (đã được bảo vệ bởi lock)
                    // ════════════════════════════════════════════════════════════════════════
                    var salesOrder = new SalesOrder
                    {
                        WarehouseId = model.WarehouseId,
                        CustomerName = model.CustomerName,
                        CustomerPhone = model.CustomerPhone,
                        CustomerAddress = model.CustomerAddress,
                        ExpectedDeliveryDate = model.ExpectedDeliveryDate,
                        CreatedBy = createdByUserId,
                        Status = "Pending",
                        TotalAmount = groupedDetails.Sum(d => d.Quantity * d.UnitPrice),
                        Notes = model.Notes,
                        CreatedAt = DateTime.Now,
                        SalesOrderDetails = groupedDetails.Select(d => new SalesOrderDetail
                        {
                            ProductId = d.ProductId,
                            Quantity = d.Quantity,
                            UnitPrice = d.UnitPrice,
                            // TotalPrice is a computed column, don't set it
                        }).ToList()
                    };

                    var createdOrder = await _salesOrderRepository
                        .CreateSalesOrderAsync(salesOrder);

                    // ════════════════════════════════════════════════════════════════════════
                    // BƯỚC 4: COMMIT transaction
                    // Lock sẽ được release sau khi commit
                    // ════════════════════════════════════════════════════════════════════════
                    await transaction.CommitAsync();

                    return (true, "Order created successfully and sent to inventory department!", 
                        createdOrder.SalesOrderId);
                }
                catch
                {
                    // Rollback nếu có lỗi trong transaction
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Log the inner exception details
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                var detailedMessage = $"Error creating sales order: {innerMessage}";
                
                // Return detailed error for debugging
                return (false, detailedMessage, null);
            }
        }

        public async Task<List<ProductSelectViewModel>> GetProductsForSelectionAsync(int warehouseId)
        {
            // Get products that have inventory in this warehouse
            var warehouseProducts = await _context.Inventories
                .Where(i => i.Location.WarehouseId == warehouseId && i.Quantity > 0)
                .Select(i => i.ProductId)
                .Distinct()
                .ToListAsync();
            
            var products = await _context.Products
                .Where(p => warehouseProducts.Contains(p.ProductId))
                .ToListAsync();

            return products.Select(p => new ProductSelectViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                SellingPrice = p.SellingPrice ?? 0,
                Unit = p.Unit ?? "Piece"
            }).ToList();
        }

        public async Task<bool> CancelSalesOrderAsync(int salesOrderId)
        {
            return await _salesOrderRepository.UpdateSalesOrderStatusAsync(salesOrderId, "Canceled");
        }

        public async Task<(bool Found, string? Name, string? Address)> GetCustomerByPhoneAsync(string phone)
        {
            var result = await _salesOrderRepository.GetLatestCustomerByPhoneAsync(phone);
            return result.HasValue ? (true, result.Value.Name, result.Value.Address) : (false, null, null);
        }
    }
}