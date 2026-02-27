using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories;
using EWMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class StockOutReceiptService : IStockOutReceiptService
    {
        private readonly IStockOutReceiptRepository _stockOutReceiptRepository;
        private readonly ISalesOrderRepository _salesOrderRepository;
        private readonly ILocationRepository _locationRepository;

        public StockOutReceiptService(
            IStockOutReceiptRepository stockOutReceiptRepository,
            ISalesOrderRepository salesOrderRepository,
            ILocationRepository locationRepository)
        {
            _stockOutReceiptRepository = stockOutReceiptRepository;
            _salesOrderRepository = salesOrderRepository;
            _locationRepository = locationRepository;
        }

        public async Task<StockOutReceiptListViewModel> GetStockOutReceiptsByWarehouseAsync(int warehouseId)
        {
            var receipts = await _stockOutReceiptRepository.GetStockOutReceiptsByWarehouseAsync(warehouseId);

            var viewModel = new StockOutReceiptListViewModel
            {
                WarehouseId = warehouseId,
                WarehouseName = receipts.FirstOrDefault()?.Warehouse.WarehouseName ?? "Unknown",
                Receipts = receipts.Select(r => new StockOutReceiptViewModel
                {
                    StockOutId = r.StockOutId,
                    ReceiptNumber = $"STOCKOUT{r.StockOutId:D4}",
                    SalesOrderId = r.SalesOrderId,
                    OrderNumber = r.SalesOrderId.HasValue ? $"SO{r.SalesOrderId.Value:D4}" : null,
                    CustomerName = r.SalesOrder?.CustomerName ?? "N/A",
                    CustomerPhone = r.SalesOrder?.CustomerPhone,
                    CustomerAddress = r.SalesOrder?.CustomerAddress,
                    IssuedDate = r.IssuedDate,
                    Reason = r.Reason,
                    Notes = r.SalesOrder?.Notes,
                    Status = r.SalesOrder?.Status,
                    TotalAmount = r.TotalAmount,
                    WarehouseName = r.Warehouse.WarehouseName,
                    IssuedByName = r.IssuedByNavigation.FullName ?? r.IssuedByNavigation.Username,
                    CreatedAt = r.CreatedAt,
                    Details = r.StockOutDetails.Select(d => new StockOutDetailViewModel
                    {
                        StockOutDetailId = d.StockOutDetailId,
                        ProductId = d.ProductId,
                        ProductName = d.Product.ProductName,
                        LocationId = d.LocationId,
                        LocationCode = d.Location.LocationCode,
                        LocationName = d.Location.LocationName,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        TotalPrice = d.TotalPrice ?? 0,
                        Unit = d.Product.Unit
                    }).ToList()
                }).ToList()
            };

            return viewModel;
        }

        public async Task<StockOutReceiptViewModel?> GetStockOutReceiptByIdAsync(int stockOutId)
        {
            var receipt = await _stockOutReceiptRepository.GetStockOutReceiptByIdAsync(stockOutId);
            if (receipt == null) return null;

            return new StockOutReceiptViewModel
            {
                StockOutId = receipt.StockOutId,
                ReceiptNumber = $"STOCKOUT{receipt.StockOutId:D4}",
                SalesOrderId = receipt.SalesOrderId,
                OrderNumber = receipt.SalesOrderId.HasValue ? $"SO{receipt.SalesOrderId.Value:D4}" : null,
                CustomerName = receipt.SalesOrder?.CustomerName ?? "N/A",
                CustomerPhone = receipt.SalesOrder?.CustomerPhone,
                CustomerAddress = receipt.SalesOrder?.CustomerAddress,
                IssuedDate = receipt.IssuedDate,
                Reason = receipt.Reason,
                Notes = receipt.SalesOrder?.Notes,
                Status = receipt.SalesOrder?.Status,
                TotalAmount = receipt.TotalAmount,
                WarehouseName = receipt.Warehouse.WarehouseName,
                IssuedByName = receipt.IssuedByNavigation.FullName ?? receipt.IssuedByNavigation.Username,
                CreatedAt = receipt.CreatedAt,
                Details = receipt.StockOutDetails.Select(d => new StockOutDetailViewModel
                {
                    StockOutDetailId = d.StockOutDetailId,
                    ProductId = d.ProductId,
                    ProductName = d.Product.ProductName,
                    LocationId = d.LocationId,
                    LocationCode = d.Location.LocationCode,
                    LocationName = d.Location.LocationName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TotalPrice = d.TotalPrice ?? 0,
                    Unit = d.Product.Unit
                }).ToList()
            };
        }

        public async Task<List<SalesOrderForStockOutViewModel>> GetPendingSalesOrdersAsync(int warehouseId)
        {
            var orders = await _salesOrderRepository.GetSalesOrdersByWarehouseAsync(warehouseId);

            var pendingOrders = orders
                .Where(o => o.Status == "Pending" || o.Status == "Partial")
                .Select(o => new SalesOrderForStockOutViewModel
                {
                    SalesOrderId = o.SalesOrderId,
                    OrderNumber = $"SO{o.SalesOrderId:D4}",
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    CustomerAddress = o.CustomerAddress,
                    ExpectedDeliveryDate = o.CreatedAt.AddDays(3), // Use CreatedAt + 3 days
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    Notes = o.Notes,
                    CreatedAt = o.CreatedAt,
                    WarehouseName = o.Warehouse.WarehouseName,
                    HasStockOutReceipt = o.StockOutReceipts.Any(),
                    StockOutReceiptId = o.StockOutReceipts.FirstOrDefault()?.StockOutId,
                    Details = o.SalesOrderDetails.Select(d => new SalesOrderDetailViewModel
                    {
                        ProductId = d.ProductId,
                        ProductName = d.Product.ProductName,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        TotalPrice = d.TotalPrice ?? 0,
                        Unit = d.Product.Unit ?? ""
                    }).ToList()
                }).ToList();

            return pendingOrders;
        }

        public async Task<StockOutOrderListViewModel> GetPendingOrdersForIndexAsync(int warehouseId, string? customer, string? status, int page, int pageSize)
        {
            var orders = await _salesOrderRepository.GetSalesOrdersByWarehouseAsync(warehouseId);

            var filtered = orders.Where(o => o.Status == "Pending" || o.Status == "Partial");

            if (!string.IsNullOrWhiteSpace(customer))
            {
                var lc = customer.Trim().ToLower();
                filtered = filtered.Where(o => (o.CustomerName != null && o.CustomerName.ToLower().Contains(lc))
                                            || (o.CustomerPhone != null && o.CustomerPhone.ToLower().Contains(lc)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(o => o.Status == status);
            }

            var totalCount = filtered.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pageItems = filtered
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var list = pageItems.Select(o => new SalesOrderForStockOutViewModel
            {
                SalesOrderId = o.SalesOrderId,
                OrderNumber = $"SO{o.SalesOrderId:D4}",
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                CustomerAddress = o.CustomerAddress,
                ExpectedDeliveryDate = o.CreatedAt.AddDays(3), // Use CreatedAt + 3 days
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                Notes = o.Notes,
                CreatedAt = o.CreatedAt,
                WarehouseName = o.Warehouse.WarehouseName,
                HasStockOutReceipt = o.StockOutReceipts.Any(),
                StockOutReceiptId = o.StockOutReceipts.FirstOrDefault()?.StockOutId,
                Details = o.SalesOrderDetails.Select(d => new SalesOrderDetailViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TotalPrice = d.TotalPrice ?? 0,
                    Unit = d.Product.Unit ?? ""
                }).ToList()
            }).ToList();

            return new StockOutOrderListViewModel
            {
                WarehouseId = warehouseId,
                WarehouseName = orders.FirstOrDefault()?.Warehouse.WarehouseName ?? "Unknown",
                Orders = list,
                FilterCustomer = customer ?? string.Empty,
                FilterStatus = status ?? string.Empty,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        public async Task<SalesOrderForStockOutViewModel?> GetSalesOrderForStockOutAsync(int salesOrderId)
        {
            var order = await _salesOrderRepository.GetSalesOrderByIdAsync(salesOrderId);
            if (order == null) return null;

            var existingReceipt = await _stockOutReceiptRepository.GetStockOutReceiptBySalesOrderIdAsync(salesOrderId);

            var details = new List<SalesOrderDetailViewModel>();
            foreach (var d in order.SalesOrderDetails)
            {
                var shipped = await _stockOutReceiptRepository.GetShippedQuantityAsync(order.SalesOrderId, d.ProductId);
                var remaining = Math.Max(0, d.Quantity - shipped);
                if (remaining > 0)
                {
                    details.Add(new SalesOrderDetailViewModel
                    {
                        ProductId = d.ProductId,
                        ProductName = d.Product.ProductName,
                        Quantity = remaining,
                        UnitPrice = d.UnitPrice,
                        TotalPrice = d.UnitPrice * remaining,
                        Unit = d.Product.Unit ?? ""
                    });
                }
            }

            return new SalesOrderForStockOutViewModel
            {
                SalesOrderId = order.SalesOrderId,
                OrderNumber = $"SO{order.SalesOrderId:D4}",
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerAddress = order.CustomerAddress,
                ExpectedDeliveryDate = order.CreatedAt.AddDays(3), // Use CreatedAt + 3 days
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                WarehouseName = order.Warehouse.WarehouseName,
                HasStockOutReceipt = existingReceipt != null,
                StockOutReceiptId = existingReceipt?.StockOutId,
                Details = details
            };
        }

        public async Task<List<LocationInventoryDto>> GetAvailableLocationsForProductAsync(int warehouseId, int productId)
        {
            return await _locationRepository.GetLocationInventoryByProductAsync(warehouseId, productId);
        }

        public async Task<(bool Success, string Message, int? ReceiptId)> CreateStockOutReceiptAsync(
            CreateStockOutReceiptViewModel model,
            int issuedByUserId)
        {
            var salesOrder = await _salesOrderRepository.GetSalesOrderByIdAsync(model.SalesOrderId);
            if (salesOrder == null)
            {
                return (false, "Order not found!", null);
            }

            foreach (var detail in model.Details)
            {
                var ordered = salesOrder.SalesOrderDetails.FirstOrDefault(d => d.ProductId == detail.ProductId)?.Quantity ?? 0;
                var shippedSoFar = await _stockOutReceiptRepository.GetShippedQuantityAsync(model.SalesOrderId, detail.ProductId);
                var remaining = Math.Max(0, ordered - shippedSoFar);

                if (detail.Quantity < 1 || detail.Quantity > remaining)
                {
                    return (false, $"Invalid quantity for product {detail.ProductId}. Remaining: {remaining}.", null);
                }

                var availableQty = await _locationRepository.GetAvailableQuantityAsync(detail.ProductId, detail.LocationId);
                if (availableQty < detail.Quantity)
                {
                    return (false, $"Insufficient stock at selected location! Need {detail.Quantity} but only have {availableQty}.", null);
                }
            }

            var stockOutReceipt = new StockOutReceipt
            {
                WarehouseId = model.WarehouseId,
                IssuedBy = issuedByUserId,
                IssuedDate = model.IssuedDate,
                Reason = model.Reason ?? "Sale",
                SalesOrderId = model.SalesOrderId,
                TotalAmount = model.Details.Sum(d => d.Quantity * d.UnitPrice),
                CreatedAt = DateTime.Now,
                StockOutDetails = model.Details
                    .Where(d => d.Quantity > 0)
                    .Select(d => new StockOutDetail
                    {
                        ProductId = d.ProductId,
                        LocationId = d.LocationId,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        TotalPrice = d.Quantity * d.UnitPrice
                    }).ToList()
            };

            var createdReceipt = await _stockOutReceiptRepository.CreateStockOutReceiptAsync(stockOutReceipt);

            foreach (var detail in model.Details)
            {
                var updateResult = await _stockOutReceiptRepository.UpdateInventoryAsync(
                    detail.ProductId,
                    detail.LocationId,
                    detail.Quantity);

                if (!updateResult)
                {
                    return (false, "Error updating inventory!", null);
                }
            }

            var refreshedOrder = await _salesOrderRepository.GetSalesOrderByIdAsync(model.SalesOrderId);
            if (refreshedOrder == null)
            {
                return (false, "Order not found after creation!", null);
            }

            bool allCompleted = true;
            bool anyShipped = false;
            foreach (var od in refreshedOrder.SalesOrderDetails)
            {
                var shipped = await _stockOutReceiptRepository.GetShippedQuantityAsync(model.SalesOrderId, od.ProductId);
                if (shipped > 0) anyShipped = true;
                if (shipped < od.Quantity) allCompleted = false;
            }

            var newStatus = allCompleted ? "Completed" : (anyShipped ? "Partial" : refreshedOrder.Status);
            var updateStatusResult = await _salesOrderRepository.UpdateSalesOrderStatusAsync(model.SalesOrderId, newStatus);
            if (!updateStatusResult)
            {
                return (false, "Error updating order status!", null);
            }

            return (true, "Stock out receipt created successfully!", createdReceipt.StockOutId);
        }
    }
}
