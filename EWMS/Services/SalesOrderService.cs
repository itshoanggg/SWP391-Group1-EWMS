using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories;
using EWMS.Repositories.Interfaces;
using EWMS.ViewModels;

namespace EWMS.Services
{
    public class SalesOrderService : ISalesOrderService
    {
        private readonly ISalesOrderRepository _salesOrderRepository;
        private readonly Repositories.IProductRepository _productRepository;
        private readonly IInventoryCheckService _inventoryCheckService;
        private readonly IWarehouseRepository _warehouseRepository;

        public SalesOrderService(
            ISalesOrderRepository salesOrderRepository,
            Repositories.IProductRepository productRepository,
            IInventoryCheckService inventoryCheckService,
            IWarehouseRepository warehouseRepository)
        {
            _salesOrderRepository = salesOrderRepository;
            _productRepository = productRepository;
            _inventoryCheckService = inventoryCheckService;
            _warehouseRepository = warehouseRepository;
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

            return (true, "Order created successfully and sent to inventory department!", createdOrder.SalesOrderId);
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