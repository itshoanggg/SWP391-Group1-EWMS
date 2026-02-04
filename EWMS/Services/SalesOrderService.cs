using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories;
using EWMS.ViewModels;

namespace EWMS.Services
{
    public class SalesOrderService : ISalesOrderService
    {
        private readonly ISalesOrderRepository _salesOrderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryCheckService _inventoryCheckService;

        public SalesOrderService(
            ISalesOrderRepository salesOrderRepository,
            IProductRepository productRepository,
            IInventoryCheckService inventoryCheckService)
        {
            _salesOrderRepository = salesOrderRepository;
            _productRepository = productRepository;
            _inventoryCheckService = inventoryCheckService;
        }

        public async Task<SalesOrderListViewModel> GetSalesOrdersByWarehouseAsync(int warehouseId)
        {
            var orders = await _salesOrderRepository.GetSalesOrdersByWarehouseAsync(warehouseId);

            var viewModel = new SalesOrderListViewModel
            {
                WarehouseId = warehouseId,
                WarehouseName = orders.FirstOrDefault()?.Warehouse.WarehouseName ?? "Unknown",
                Orders = orders.Select(o => new SalesOrderViewModel
                {
                    SalesOrderId = o.SalesOrderId,
                    OrderNumber = $"SO{o.SalesOrderId:D4}",
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    CustomerAddress = o.CustomerAddress,
                    ExpectedDeliveryDate = o.ExpectedDeliveryDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    WarehouseName = o.Warehouse.WarehouseName,
                    Details = o.SalesOrderDetails.Select(d => new SalesOrderDetailViewModel
                    {
                        ProductId = d.ProductId,
                        ProductName = d.Product.ProductName,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        TotalPrice = d.TotalPrice ?? 0,
                        Unit = d.Product.Unit ?? ""
                    }).ToList()
                }).ToList()
            };

            return viewModel;
        }

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
                CreatedAt = order.CreatedAt,
                WarehouseName = order.Warehouse.WarehouseName,
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
            // Bước 1: Kiểm tra tồn kho
            var inventoryCheckRequest = new InventoryCheckRequest
            {
                WarehouseId = model.WarehouseId,
                ExpectedDeliveryDate = model.ExpectedDeliveryDate,
                Products = model.Details.Select(d => new ProductQuantityDto
                {
                    ProductId = d.ProductId,
                    Quantity = d.Quantity
                }).ToList()
            };

            var checkResult = await _inventoryCheckService.CheckInventoryAvailabilityAsync(inventoryCheckRequest);

            if (!checkResult.IsValid)
            {
                return (false, checkResult.Message, null);
            }

            // Bước 2: Tạo Sales Order
            var salesOrder = new SalesOrder
            {
                WarehouseId = model.WarehouseId,
                CustomerName = model.CustomerName,
                CustomerPhone = model.CustomerPhone,
                CustomerAddress = model.CustomerAddress,
                CreatedBy = createdByUserId,
                Status = "Pending",
                ExpectedDeliveryDate = model.ExpectedDeliveryDate,
                TotalAmount = model.Details.Sum(d => d.Quantity * d.UnitPrice),
                Notes = model.Notes,
                CreatedAt = DateTime.Now,
                SalesOrderDetails = model.Details.Select(d => new SalesOrderDetail
                {
                    ProductId = d.ProductId,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice
                }).ToList()
            };

            var createdOrder = await _salesOrderRepository.CreateSalesOrderAsync(salesOrder);

            return (true, "Đơn hàng đã được tạo thành công và gửi đến bộ phận kho!", createdOrder.SalesOrderId);
        }

        public async Task<List<ProductSelectViewModel>> GetProductsForSelectionAsync()
        {
            var products = await _productRepository.GetAllProductsAsync();

            return products.Select(p => new ProductSelectViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                SellingPrice = p.SellingPrice ?? 0,
                Unit = p.Unit ?? "Piece"
            }).ToList();
        }
    }
}