using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class StockInService : IStockInService
    {
        private readonly IUnitOfWork _unitOfWork;

        public StockInService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<PurchaseOrderListDTO>> GetPurchaseOrdersForStockInAsync(int warehouseId, string? status, string? search)
        {
            // Removed automatic status update - status should be manually updated
            // await _unitOfWork.PurchaseOrders.UpdateToReadyToReceiveAsync(warehouseId);
            // await _unitOfWork.SaveChangesAsync();

            var purchaseOrders = await _unitOfWork.PurchaseOrders.GetByWarehouseIdAsync(warehouseId, status);

            // If no specific status requested, show only active statuses on the main list
            if (string.IsNullOrEmpty(status))
            {
                purchaseOrders = purchaseOrders.Where(po => po.Status == "Ordered" || po.Status == "PartiallyReceived");
            }

            if (!string.IsNullOrEmpty(search))
            {
                purchaseOrders = purchaseOrders.Where(po =>
                    po.PurchaseOrderId.ToString().Contains(search) ||
                    po.Supplier.SupplierName.Contains(search));
            }

            return purchaseOrders.Select(po =>
            {
                var totalItems = po.PurchaseOrderDetails.Sum(d => d.Quantity);
                var receivedItems = po.StockInReceipts
                    .SelectMany(si => si.StockInDetails)
                    .Sum(sid => sid.Quantity);

                return new PurchaseOrderListDTO
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    SupplierName = po.Supplier.SupplierName,
                    ExpectedReceivingDate = po.ExpectedReceivingDate,
                    TotalItems = totalItems,
                    ReceivedItems = receivedItems,
                    RemainingItems = totalItems - receivedItems,
                    TotalAmount = po.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0),
                    CreatedBy = po.CreatedByNavigation.FullName ?? po.CreatedByNavigation.Username,
                    Status = po.Status ?? "Unknown",
                    CreatedAt = po.CreatedAt
                };
            }).ToList();
        }

        public async Task<PurchaseOrder?> GetPurchaseOrderDetailsAsync(int purchaseOrderId, int warehouseId)
        {
            return await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(purchaseOrderId, warehouseId);
        }

        public async Task<PurchaseOrderInfoDTO?> GetPurchaseOrderInfoAsync(int purchaseOrderId, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(purchaseOrderId, warehouseId);

            if (purchaseOrder == null)
                return null;

            return new PurchaseOrderInfoDTO
            {
                PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                SupplierName = purchaseOrder.Supplier.SupplierName,
                SupplierId = purchaseOrder.Supplier.SupplierId,
                SupplierPhone = purchaseOrder.Supplier.Phone,
                CreatedBy = purchaseOrder.CreatedByNavigation?.FullName ?? purchaseOrder.CreatedByNavigation?.Username ?? "Unknown",
                CreatedAt = purchaseOrder.CreatedAt,
                ExpectedReceivingDate = purchaseOrder.ExpectedReceivingDate,
                Status = purchaseOrder.Status ?? "Unknown",
                HasStockIn = purchaseOrder.Status == "Received"
            };
        }

        public async Task<IEnumerable<PurchaseOrderProductDTO>> GetPurchaseOrderProductsAsync(int purchaseOrderId, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(purchaseOrderId, warehouseId);

            if (purchaseOrder == null)
                return new List<PurchaseOrderProductDTO>();

            var receivedMap = await _unitOfWork.StockIns.GetReceivedQuantitiesAsync(purchaseOrderId);

            return purchaseOrder.PurchaseOrderDetails.Select(pod =>
            {
                var receivedQty = receivedMap.ContainsKey(pod.ProductId) ? receivedMap[pod.ProductId] : 0;
                var remainingQty = pod.Quantity - receivedQty;

                return new PurchaseOrderProductDTO
                {
                    ProductId = pod.ProductId,
                    Sku = $"SKU-{pod.ProductId:D5}",
                    ProductName = pod.Product.ProductName,
                    CategoryName = pod.Product.Category?.CategoryName ?? "N/A",
                    Quantity = pod.Quantity,
                    OrderedQty = pod.Quantity,
                    ReceivedQty = receivedQty,
                    RemainingQty = remainingQty < 0 ? 0 : remainingQty,
                    UnitPrice = pod.UnitPrice
                };
            }).ToList();
        }

        public async Task<StockInReceipt> ConfirmStockInAsync(ConfirmStockInRequest request, int userId)
        {
            // Validate: Check if received quantities exceed ordered quantities
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(request.PurchaseOrderId, request.WarehouseId);
            if (purchaseOrder == null)
            {
                throw new Exception("Purchase Order not found");
            }

            var receivedQuantities = await _unitOfWork.StockIns.GetReceivedQuantitiesAsync(request.PurchaseOrderId);

            foreach (var item in request.Items)
            {
                var orderDetail = purchaseOrder.PurchaseOrderDetails.FirstOrDefault(pod => pod.ProductId == item.ProductId);
                if (orderDetail == null)
                {
                    throw new Exception($"Product {item.ProductId} not found in Purchase Order");
                }

                var previouslyReceived = receivedQuantities.ContainsKey(item.ProductId) ? receivedQuantities[item.ProductId] : 0;
                var totalReceivingForProduct = request.Items.Where(i => i.ProductId == item.ProductId).Sum(i => i.Quantity);
                var totalAfterStockIn = previouslyReceived + totalReceivingForProduct;

                if (totalAfterStockIn > orderDetail.Quantity)
                {
                    throw new Exception($"Cannot receive {totalReceivingForProduct} units of {orderDetail.Product.ProductName}. " +
                                      $"Ordered: {orderDetail.Quantity}, Previously received: {previouslyReceived}, " +
                                      $"Remaining: {orderDetail.Quantity - previouslyReceived}");
                }
            }

            var stockInReceipt = new StockInReceipt
            {
                WarehouseId = request.WarehouseId,
                ReceivedBy = userId,
                ReceivedDate = DateTime.Now,
                Reason = "Purchase",
                PurchaseOrderId = request.PurchaseOrderId,
                CreatedAt = DateTime.Now,
                TotalAmount = 0
            };

            await _unitOfWork.StockIns.AddAsync(stockInReceipt);
            await _unitOfWork.SaveChangesAsync();

            decimal totalAmount = 0;

            foreach (var item in request.Items)
            {
                var stockInDetail = new StockInDetail
                {
                    StockInId = stockInReceipt.StockInId,
                    ProductId = item.ProductId,
                    LocationId = item.LocationId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                };

                await _unitOfWork.StockIns.Context.StockInDetails.AddAsync(stockInDetail);
                totalAmount += item.Quantity * item.UnitPrice;

                // Update product prices using Moving Weighted Average BEFORE updating inventory
                // (only for Purchase orders)
                await _unitOfWork.Products.UpdateProductPricesByMovingAverageAsync(
                    item.ProductId, 
                    item.Quantity, 
                    item.UnitPrice);

                // Update inventory AFTER price calculation
                var inventory = await _unitOfWork.Inventories.GetByProductAndLocationAsync(item.ProductId, item.LocationId);

                if (inventory != null)
                {
                    inventory.Quantity = (inventory.Quantity ?? 0) + item.Quantity;
                    inventory.LastUpdated = DateTime.Now;
                }
                else
                {
                    inventory = new Inventory
                    {
                        ProductId = item.ProductId,
                        LocationId = item.LocationId,
                        Quantity = item.Quantity,
                        LastUpdated = DateTime.Now
                    };
                    await _unitOfWork.Inventories.AddAsync(inventory);
                }
            }

            stockInReceipt.TotalAmount = totalAmount;

            // Update PO status - reuse purchaseOrder and receivedQuantities from validation above
            bool fullyReceived = true;
            foreach (var pod in purchaseOrder.PurchaseOrderDetails)
            {
                var totalReceived = receivedQuantities.ContainsKey(pod.ProductId) ? receivedQuantities[pod.ProductId] : 0;
                var currentReceiving = request.Items.Where(i => i.ProductId == pod.ProductId).Sum(i => i.Quantity);
                totalReceived += currentReceiving;

                if (totalReceived < pod.Quantity)
                {
                    fullyReceived = false;
                    break;
                }
            }

            purchaseOrder.Status = fullyReceived ? "Received" : "PartiallyReceived";

            await _unitOfWork.SaveChangesAsync();
            return stockInReceipt;
        }

        public async Task<IEnumerable<AvailableLocationDTO>> GetAvailableLocationsAsync(int warehouseId, int productId)
        {
            var locations = await _unitOfWork.Locations.GetByWarehouseIdAsync(warehouseId);

            var result = new List<AvailableLocationDTO>();

            foreach (var location in locations)
            {
                var currentStock = await _unitOfWork.Inventories.Context.Inventories
                    .Where(i => i.LocationId == location.LocationId)
                    .SumAsync(i => i.Quantity) ?? 0;

                result.Add(new AvailableLocationDTO
                {
                    LocationId = location.LocationId,
                    LocationCode = location.LocationCode,
                    LocationName = location.LocationName,
                    Rack = location.Rack ?? "N/A",
                    MaxCapacity = location.Capacity,
                    CurrentStock = currentStock
                });
            }

            return result;
        }

        public async Task<LocationCapacityDTO?> CheckLocationCapacityAsync(int locationId)
        {
            var location = await _unitOfWork.Locations.GetByIdAsync(locationId);

            if (location == null)
                return null;

            var currentStock = await _unitOfWork.Inventories.Context.Inventories
                .Where(i => i.LocationId == locationId)
                .SumAsync(i => i.Quantity) ?? 0;

            return new LocationCapacityDTO
            {
                LocationId = location.LocationId,
                LocationCode = location.LocationCode,
                MaxCapacity = location.Capacity,
                CurrentStock = currentStock
            };
        }

        public async Task<IEnumerable<PurchaseOrderListDTO>> GetPurchaseOrdersHistoryAsync(int warehouseId, string? search)
        {
            var purchaseOrders = await _unitOfWork.PurchaseOrders.GetByWarehouseIdAsync(warehouseId, null);

            // Only historical statuses
            purchaseOrders = purchaseOrders.Where(po => po.Status == "Received" || po.Status == "Cancelled");

            if (!string.IsNullOrEmpty(search))
            {
                purchaseOrders = purchaseOrders.Where(po =>
                    po.PurchaseOrderId.ToString().Contains(search) ||
                    po.Supplier.SupplierName.Contains(search));
            }

            var list = purchaseOrders.Select(po =>
            {
                var totalItems = po.PurchaseOrderDetails.Sum(d => d.Quantity);
                var receivedItems = po.StockInReceipts
                    .SelectMany(si => si.StockInDetails)
                    .Sum(sid => sid.Quantity);
                var lastReceived = po.StockInReceipts
                    .OrderByDescending(r => r.ReceivedDate)
                    .FirstOrDefault()?.ReceivedDate;

                return new PurchaseOrderListDTO
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    SupplierName = po.Supplier.SupplierName,
                    ExpectedReceivingDate = po.ExpectedReceivingDate,
                    TotalItems = totalItems,
                    ReceivedItems = receivedItems,
                    RemainingItems = totalItems - receivedItems,
                    TotalAmount = po.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0),
                    CreatedBy = po.CreatedByNavigation.FullName ?? po.CreatedByNavigation.Username,
                    Status = po.Status ?? "Unknown",
                    CreatedAt = po.CreatedAt,
                    LastReceivedDate = lastReceived
                };
            }).ToList();

            return list.OrderByDescending(x => x.LastReceivedDate ?? x.CreatedAt);
        }

        public async Task<List<PurchaseOrderAllocationDTO>> GetPurchaseOrderAllocationsAsync(int purchaseOrderId)
        {
            var details = await _unitOfWork.StockIns.GetDetailsByPurchaseOrderIdAsync(purchaseOrderId);
            return details.Select(d => new PurchaseOrderAllocationDTO
            {
                ProductId = d.ProductId,
                LocationId = d.LocationId,
                LocationCode = d.Location.LocationCode,
                LocationName = d.Location.LocationName,
                Quantity = d.Quantity
            }).ToList();
        }

        private async Task UpdatePurchaseOrderStatusAsync(int purchaseOrderId, List<StockInDetailViewModel> details)
        {
            var receivedQuantities = await _unitOfWork.StockIns.GetReceivedQuantitiesAsync(purchaseOrderId);
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(purchaseOrderId, 0);

            if (purchaseOrder != null)
            {
                bool fullyReceived = true;
                foreach (var pod in purchaseOrder.PurchaseOrderDetails)
                {
                    var totalReceived = receivedQuantities.ContainsKey(pod.ProductId) ? receivedQuantities[pod.ProductId] : 0;
                    var currentReceiving = details.Where(d => d.ProductId == pod.ProductId).Sum(d => d.CurrentReceiving);
                    totalReceived += currentReceiving;

                    if (totalReceived < pod.Quantity)
                    {
                        fullyReceived = false;
                        break;
                    }
                }

                purchaseOrder.Status = fullyReceived ? "Received" : "PartiallyReceived";
            }
        }
    }
}
