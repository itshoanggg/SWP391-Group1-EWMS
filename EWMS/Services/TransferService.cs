using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;
using EWMS.ViewModels;

namespace EWMS.Services
{
    public class TransferService
    {
        private readonly EWMSDbContext _db;

        public TransferService(EWMSDbContext db)
        {
            _db = db;
        }

        public async Task<List<TransferRequest>> GetAllTransfersAsync()
        {
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .Include(t => t.ApprovedByNavigation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(t => t.RequestedDate)
                .ToListAsync();
        }

        public async Task<List<TransferRequest>> GetTransfersForWarehouseAsync(int warehouseId)
        {
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .Include(t => t.ApprovedByNavigation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Where(t => t.ToWarehouseId == warehouseId || t.FromWarehouseId == warehouseId)
                .OrderByDescending(t => t.RequestedDate)
                .ToListAsync();
        }

        public async Task<List<Warehouse>> GetWarehousesAsync()
        {
            return await _db.Warehouses.ToListAsync();
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            return await _db.Products.ToListAsync();
        }

        public async Task<List<string>> GetRacksByWarehouseAsync(int warehouseId)
        {
            return await _db.Locations
                .Where(l => l.WarehouseId == warehouseId && l.Rack != null)
                .Select(l => l.Rack!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
        }

        public async Task<List<LocationCapacityViewModel>> GetLocationsByRackWithCapacityAsync(int warehouseId, string rack)
        {
            var locations = await _db.Locations
                .Where(l => l.WarehouseId == warehouseId && l.Rack == rack)
                .ToListAsync();

            var result = new List<LocationCapacityViewModel>();
            foreach (var loc in locations)
            {
                var currentStock = await _db.Inventories
                    .Where(i => i.LocationId == loc.LocationId)
                    .SumAsync(i => i.Quantity ?? 0);

                result.Add(new LocationCapacityViewModel
                {
                    LocationId = loc.LocationId,
                    LocationCode = loc.LocationCode,
                    LocationName = loc.LocationName,
                    Rack = loc.Rack,
                    Capacity = loc.Capacity,
                    CurrentStock = currentStock,
                    AvailableSpace = Math.Max(0, loc.Capacity - currentStock)
                });
            }

            return result.OrderBy(r => r.LocationCode).ToList();
        }

        public async Task<List<ProductStockLocationViewModel>> GetProductStockByLocationAsync(int warehouseId, int productId)
        {
            return await _db.Inventories
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId && i.Location.WarehouseId == warehouseId && (i.Quantity ?? 0) > 0)
                .Select(i => new ProductStockLocationViewModel
                {
                    LocationId = i.LocationId,
                    LocationCode = i.Location.LocationCode,
                    LocationName = i.Location.LocationName,
                    Rack = i.Location.Rack,
                    AvailableQuantity = i.Quantity ?? 0
                })
                .OrderBy(x => x.Rack)
                .ThenBy(x => x.LocationCode)
                .ToListAsync();
        }

        public async Task<LocationCapacityViewModel> GetLocationCapacityAsync(int locationId, int productId)
        {
            var location = await _db.Locations.FindAsync(locationId);
            if (location == null)
            {
                throw new InvalidOperationException("Location not found.");
            }

            var currentStock = await _db.Inventories
                .Where(i => i.LocationId == locationId)
                .SumAsync(i => i.Quantity ?? 0);

            var productStock = await _db.Inventories
                .Where(i => i.LocationId == locationId && i.ProductId == productId)
                .Select(i => i.Quantity ?? 0)
                .FirstOrDefaultAsync();

            return new LocationCapacityViewModel
            {
                LocationId = location.LocationId,
                LocationCode = location.LocationCode,
                LocationName = location.LocationName,
                Rack = location.Rack,
                Capacity = location.Capacity,
                CurrentStock = currentStock,
                AvailableSpace = Math.Max(0, location.Capacity - currentStock)
            };
        }

        public async Task<TransferRequest?> GetTransferByIdAsync(int id)
        {
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .Include(t => t.ApprovedByNavigation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.FromLocation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.ToLocation)
                .FirstOrDefaultAsync(t => t.TransferId == id);
        }

        public async Task<List<TransferProductStockViewModel>> GetAvailableProductsByWarehouseAsync(int warehouseId)
        {
            return await _db.Inventories
                .Include(i => i.Product)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId && (i.Quantity ?? 0) > 0)
                .GroupBy(i => new
                {
                    i.ProductId,
                    i.Product.ProductName
                })
                .Select(g => new TransferProductStockViewModel
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    Sku = $"SKU-{g.Key.ProductId:D5}",
                    AvailableQuantity = g.Sum(x => x.Quantity ?? 0)
                })
                .OrderBy(x => x.ProductName)
                .ToListAsync();
        }

        public async Task<int> CreateCompleteTransferAsync(CreateCompleteTransferRequest model, int userId)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // Validate warehouses
                if (model.FromWarehouseId == model.ToWarehouseId)
                {
                    throw new InvalidOperationException("Source and destination warehouses must be different.");
                }

                var fromWarehouse = await _db.Warehouses.FindAsync(model.FromWarehouseId);
                var toWarehouse = await _db.Warehouses.FindAsync(model.ToWarehouseId);
                if (fromWarehouse == null || toWarehouse == null)
                {
                    throw new InvalidOperationException("Invalid warehouse selection.");
                }

                // Group items by product for validation
                var itemsByProduct = model.Items.GroupBy(x => x.ProductId).ToList();

                foreach (var productGroup in itemsByProduct)
                {
                    var productId = productGroup.Key;
                    var product = await _db.Products.FindAsync(productId);
                    if (product == null)
                    {
                        throw new InvalidOperationException($"Product ID {productId} not found.");
                    }

                    // Validate: total quantity per product doesn't exceed available stock at source
                    var totalAvailable = await _db.Inventories
                        .Include(i => i.Location)
                        .Where(i => i.ProductId == productId && i.Location.WarehouseId == model.FromWarehouseId)
                        .SumAsync(i => i.Quantity ?? 0);

                    var totalRequested = productGroup.Sum(x => x.Quantity);
                    if (totalRequested > totalAvailable)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for '{product.ProductName}'. Available: {totalAvailable}, Requested: {totalRequested}");
                    }

                    foreach (var item in productGroup)
                    {
                        // Validate source location
                        var sourceLocation = await _db.Locations
                            .FirstOrDefaultAsync(l => l.LocationId == item.FromLocationId && l.WarehouseId == model.FromWarehouseId);
                        if (sourceLocation == null)
                        {
                            throw new InvalidOperationException($"Source location ID {item.FromLocationId} does not belong to the source warehouse.");
                        }

                        var sourceInventory = await _db.Inventories
                            .FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == item.FromLocationId);
                        if (sourceInventory == null || (sourceInventory.Quantity ?? 0) < item.Quantity)
                        {
                            var availableAtLoc = sourceInventory?.Quantity ?? 0;
                            throw new InvalidOperationException(
                                $"Insufficient stock for '{product.ProductName}' at {sourceLocation.LocationCode}. Available: {availableAtLoc}, Requested: {item.Quantity}");
                        }

                        // Validate destination location
                        var destLocation = await _db.Locations
                            .FirstOrDefaultAsync(l => l.LocationId == item.ToLocationId && l.WarehouseId == model.ToWarehouseId);
                        if (destLocation == null)
                        {
                            throw new InvalidOperationException($"Destination location ID {item.ToLocationId} does not belong to the destination warehouse.");
                        }

                        // Validate destination rack capacity
                        var destCurrentStock = await _db.Inventories
                            .Where(i => i.LocationId == item.ToLocationId)
                            .SumAsync(i => i.Quantity ?? 0);
                        var destAvailableSpace = destLocation.Capacity - destCurrentStock;
                        if (item.Quantity > destAvailableSpace)
                        {
                            throw new InvalidOperationException(
                                $"Destination rack '{destLocation.LocationCode}' does not have enough capacity. Available space: {destAvailableSpace}, Trying to add: {item.Quantity}");
                        }
                    }
                }

                // Create TransferRequest with Pending status
                var transfer = new TransferRequest
                {
                    FromWarehouseId = model.FromWarehouseId,
                    ToWarehouseId = model.ToWarehouseId,
                    TransferType = "Transfer",
                    RequestedBy = userId,
                    RequestedDate = DateTime.Now,
                    ApprovedBy = null,
                    ApprovedDate = null,
                    Status = "Pending",
                    Reason = model.Reason
                };
                _db.TransferRequests.Add(transfer);
                await _db.SaveChangesAsync();

                // Create TransferDetails
                foreach (var item in model.Items)
                {
                    _db.TransferDetails.Add(new TransferDetail
                    {
                        TransferId = transfer.TransferId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        FromLocationId = item.FromLocationId,
                        ToLocationId = item.ToLocationId
                    });
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return transfer.TransferId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<PendingTransferReceiptViewModel>> GetPendingTransferStockOutAsync(int warehouseId)
        {
            return await _db.TransferRequests
                .Include(t => t.ToWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
                .Where(t => t.FromWarehouseId == warehouseId &&
                            (t.Status == "Pending" || t.Status == "Approved" || t.Status == "Partial"))
                .OrderByDescending(t => t.RequestedDate)
                .Select(t => new PendingTransferReceiptViewModel
                {
                    TransferId = t.TransferId,
                    WarehouseId = t.FromWarehouseId,
                    WarehouseName = t.FromWarehouse.WarehouseName,
                    CounterpartyWarehouseName = t.ToWarehouse.WarehouseName,
                    ProductSummary = string.Join(", ", t.TransferDetails.Select(d => d.Product.ProductName)),
                    TotalQuantity = t.TransferDetails.Sum(d => d.Quantity),
                    RequestedDate = t.RequestedDate,
                    Status = t.Status ?? string.Empty,
                    IsProcessed = t.StockOutReceipts.Any(r => r.StockOutDetails.Any())
                })
                .ToListAsync();
        }

        public async Task<List<PendingTransferReceiptViewModel>> GetPendingTransferStockInAsync(int warehouseId)
        {
            var transfers = await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
                .Include(t => t.StockInReceipts)
                    .ThenInclude(r => r.StockInDetails)
                .Where(t => t.ToWarehouseId == warehouseId &&
                            t.Status == "In Transit")
                .OrderByDescending(t => t.RequestedDate)
                .ToListAsync();

            return transfers.Select(t =>
            {
                // Calculate shipped quantities
                var shippedMap = t.StockOutReceipts
                    ?.SelectMany(r => r.StockOutDetails ?? new List<StockOutDetail>())
                    .GroupBy(d => d.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity))
                    ?? new Dictionary<int, int>();

                // Calculate received quantities
                var receivedMap = t.StockInReceipts
                    ?.SelectMany(r => r.StockInDetails ?? new List<StockInDetail>())
                    .GroupBy(d => d.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity))
                    ?? new Dictionary<int, int>();

                // Check if fully received
                bool fullyReceived = shippedMap.Any() && shippedMap.All(kvp =>
                    receivedMap.GetValueOrDefault(kvp.Key, 0) >= kvp.Value);

                return new PendingTransferReceiptViewModel
                {
                    TransferId = t.TransferId,
                    WarehouseId = t.ToWarehouseId ?? 0,
                    WarehouseName = t.ToWarehouse?.WarehouseName ?? "Unknown",
                    CounterpartyWarehouseName = t.FromWarehouse?.WarehouseName ?? "Unknown",
                    ProductSummary = string.Join(", ", t.TransferDetails?.Select(d => d.Product?.ProductName ?? "Unknown") ?? new List<string>()),
                    TotalQuantity = t.TransferDetails?.Sum(d => d.Quantity) ?? 0,
                    RequestedDate = t.RequestedDate,
                    Status = t.Status ?? string.Empty,
                    IsProcessed = fullyReceived // Only mark as processed when fully received
                };
            }).ToList();
        }

        public async Task<TransferStockOutPageViewModel?> GetTransferStockOutAsync(int transferId, int warehouseId)
        {
            var transfer = await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TransferId == transferId && t.FromWarehouseId == warehouseId);

            if (transfer == null || (transfer.Status != "Approved" && transfer.Status != "In Transit"))
            {
                return null;
            }

            return new TransferStockOutPageViewModel
            {
                TransferId = transfer.TransferId,
                WarehouseId = warehouseId,
                WarehouseName = transfer.FromWarehouse.WarehouseName,
                DestinationWarehouseName = transfer.ToWarehouse?.WarehouseName ?? "Unknown",
                Reason = transfer.Reason,
                Details = transfer.TransferDetails.Select(d => new TransferStockOutItemViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product.ProductName,
                    Quantity = d.Quantity,
                    Unit = d.Product.Unit ?? string.Empty,
                    UnitPrice = d.Product.SellingPrice ?? 0
                }).ToList()
            };
        }

        public async Task<SalesOrderForStockOutViewModel?> GetTransferForStockOutAsync(int transferId, int warehouseId)
        {
            var transfer = await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Inventories)
                        .ThenInclude(i => i.Location)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.FromLocation)
                .Include(t => t.StockOutReceipts)
                .FirstOrDefaultAsync(t => t.TransferId == transferId && t.FromWarehouseId == warehouseId);

            if (transfer == null)
            {
                return null;
            }

            if (transfer.Status != "Pending" && transfer.Status != "Approved")
            {
                throw new InvalidOperationException($"Cannot process stock-out for transfer with status: {transfer.Status}");
            }

            return new SalesOrderForStockOutViewModel
            {
                SalesOrderId = transferId,
                OrderNumber = $"TR-{transferId:D4}",
                CustomerName = $"Transfer to {transfer.ToWarehouse?.WarehouseName ?? "Unknown"}",
                CustomerPhone = "",
                CustomerAddress = transfer.ToWarehouse?.Address ?? "",
                ExpectedDeliveryDate = transfer.RequestedDate?.AddDays(7) ?? DateTime.Now.AddDays(7),
                TotalAmount = 0,
                Status = transfer.Status ?? "Pending",
                Notes = transfer.Reason,
                CreatedAt = transfer.RequestedDate ?? DateTime.Now,
                WarehouseName = transfer.FromWarehouse.WarehouseName,
                HasStockOutReceipt = transfer.StockOutReceipts.Any(),
                StockOutReceiptId = transfer.StockOutReceipts.FirstOrDefault()?.StockOutId,
                IsTransfer = true,
                Details = transfer.TransferDetails.Select(d => new SalesOrderDetailViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.Product.SellingPrice ?? 0,
                    TotalPrice = (d.Product.SellingPrice ?? 0) * d.Quantity,
                    Unit = d.Product.Unit ?? "",
                    PrefilledLocationId = d.FromLocationId,
                    PrefilledLocationName = d.FromLocation?.LocationName
                }).ToList()
            };
        }

        public async Task<TransferStockInPageViewModel?> GetTransferStockInAsync(int transferId, int warehouseId)
        {
            var transfer = await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
                        .ThenInclude(d => d.Product)
                .Include(t => t.StockInReceipts)
                    .ThenInclude(r => r.StockInDetails)
                .FirstOrDefaultAsync(t => t.TransferId == transferId && t.ToWarehouseId == warehouseId);

            if (transfer == null || (transfer.Status != "Approved" && transfer.Status != "In Transit"))
            {
                return null;
            }

            var stockOutDetails = transfer.StockOutReceipts
                .SelectMany(r => r.StockOutDetails)
                .ToList();

            if (!stockOutDetails.Any())
            {
                throw new InvalidOperationException("Transfer stock-out has not been processed yet.");
            }

            // Calculate shipped quantities by product
            var shippedMap = stockOutDetails
                .GroupBy(d => d.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            // Calculate already received quantities by product
            var receivedMap = transfer.StockInReceipts
                .SelectMany(r => r.StockInDetails)
                .GroupBy(d => d.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            return new TransferStockInPageViewModel
            {
                TransferId = transfer.TransferId,
                WarehouseId = warehouseId,
                WarehouseName = transfer.ToWarehouse?.WarehouseName ?? "Unknown",
                SourceWarehouseName = transfer.FromWarehouse.WarehouseName,
                Reason = transfer.Reason,
                Details = stockOutDetails
                    .GroupBy(d => d.ProductId)
                    .Select(g => new TransferStockInItemViewModel
                    {
                        ProductId = g.Key,
                        ProductName = g.First().Product.ProductName,
                        Quantity = shippedMap[g.Key],  // Total shipped
                        ReceivedQuantity = receivedMap.GetValueOrDefault(g.Key, 0), // Already received
                        RemainingQuantity = shippedMap[g.Key] - receivedMap.GetValueOrDefault(g.Key, 0), // Remaining
                        Unit = g.First().Product.Unit ?? string.Empty,
                        UnitPrice = g.First().UnitPrice
                    }).ToList()
            };
        }

        public async Task<int> ProcessTransferStockOutAsync(CreateTransferStockOutViewModel model, int userId)
        {
            var transfer = await _db.TransferRequests
                .Include(t => t.TransferDetails)
                .FirstOrDefaultAsync(t => t.TransferId == model.TransferId);

            if (transfer == null || transfer.FromWarehouseId != model.WarehouseId)
            {
                throw new InvalidOperationException("Transfer not found.");
            }

            if (transfer.Status != "Pending" && transfer.Status != "Approved" && transfer.Status != "Partial")
            {
                throw new InvalidOperationException($"Cannot process stock-out for transfer status: {transfer.Status}");
            }

            // Group transfer details by product to get total expected quantity per product
            var expectedMap = transfer.TransferDetails
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            // Group incoming details by product to get total quantity being shipped per product
            var incomingMap = model.Details
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            // Validate quantities - allow partial stock-out like sales orders
            foreach (var kvp in expectedMap)
            {
                if (!incomingMap.TryGetValue(kvp.Key, out var incomingQty))
                {
                    throw new InvalidOperationException($"Product ID {kvp.Key} is missing in the stock-out details.");
                }
                
                if (incomingQty > kvp.Value)
                {
                    throw new InvalidOperationException($"Transfer stock-out quantity for product ID {kvp.Key} exceeds expected. Expected: {kvp.Value}, Received: {incomingQty}");
                }
                
                if (incomingQty < 1)
                {
                    throw new InvalidOperationException($"Transfer stock-out quantity for product ID {kvp.Key} must be at least 1.");
                }
            }

            // Validate inventory availability for each location
            foreach (var detail in model.Details)
            {
                var inventory = await _db.Inventories
                    .Include(i => i.Location)
                    .FirstOrDefaultAsync(i => i.ProductId == detail.ProductId && i.LocationId == detail.LocationId && i.Location.WarehouseId == model.WarehouseId);

                if (inventory == null || (inventory.Quantity ?? 0) < detail.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for product ID {detail.ProductId} at the selected location.");
                }
            }

            var receipt = await _db.StockOutReceipts
                .Include(r => r.StockOutDetails)
                .FirstOrDefaultAsync(r => r.TransferId == model.TransferId);

            if (receipt == null)
            {
                receipt = new StockOutReceipt
                {
                    WarehouseId = model.WarehouseId,
                    IssuedBy = userId,
                    IssuedDate = model.IssuedDate,
                    Reason = "Transfer Out",
                    TransferId = model.TransferId,
                    CreatedAt = DateTime.Now,
                    TotalAmount = 0
                };
                _db.StockOutReceipts.Add(receipt);
                await _db.SaveChangesAsync();
            }
            else if (receipt.StockOutDetails.Any())
            {
                throw new InvalidOperationException("Transfer stock-out has already been processed.");
            }

            decimal totalAmount = 0;
            foreach (var detail in model.Details)
            {
                _db.StockOutDetails.Add(new StockOutDetail
                {
                    StockOutId = receipt.StockOutId,
                    ProductId = detail.ProductId,
                    LocationId = detail.LocationId,
                    Quantity = detail.Quantity,
                    UnitPrice = detail.UnitPrice
                });

                var inventory = await _db.Inventories.FirstAsync(i => i.ProductId == detail.ProductId && i.LocationId == detail.LocationId);
                inventory.Quantity = (inventory.Quantity ?? 0) - detail.Quantity;
                inventory.LastUpdated = DateTime.Now;

                totalAmount += detail.Quantity * detail.UnitPrice;
            }

            receipt.IssuedBy = userId;
            receipt.IssuedDate = model.IssuedDate;
            receipt.TotalAmount = totalAmount;
            
            // Check if all products have been fully shipped
            var shippedMap = receipt.StockOutDetails
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            bool fullyShipped = expectedMap.All(kvp => 
                shippedMap.TryGetValue(kvp.Key, out var shipped) && shipped >= kvp.Value);
            
            transfer.Status = fullyShipped ? "In Transit" : "Partial";

            await _db.SaveChangesAsync();
            return receipt.StockOutId;
        }

        public async Task<int> ProcessTransferStockInAsync(ConfirmTransferStockInRequest request, int userId)
        {
            var transfer = await _db.TransferRequests
                .Include(t => t.TransferDetails)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
                .FirstOrDefaultAsync(t => t.TransferId == request.TransferId);

            if (transfer == null || transfer.ToWarehouseId != request.WarehouseId)
            {
                throw new InvalidOperationException("Transfer not found.");
            }

            if (transfer.Status != "In Transit")
            {
                throw new InvalidOperationException($"Cannot process stock-in for transfer status: {transfer.Status}");
            }

            var stockOutDetails = transfer.StockOutReceipts
                .SelectMany(r => r.StockOutDetails)
                .ToList();

            if (!stockOutDetails.Any())
            {
                throw new InvalidOperationException("Transfer stock-out has not been processed yet.");
            }

            // Get total shipped quantities by product
            var shippedMap = stockOutDetails.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            // Get already received quantities by product
            var alreadyReceivedMap = transfer.StockInReceipts
                .SelectMany(r => r.StockInDetails)
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            // Get incoming quantities by product
            var incomingMap = request.Items.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            // Validate quantities - allow partial receiving
            foreach (var kvp in incomingMap)
            {
                var productId = kvp.Key;
                var incomingQty = kvp.Value;
                var shippedQty = shippedMap.GetValueOrDefault(productId, 0);
                var alreadyReceived = alreadyReceivedMap.GetValueOrDefault(productId, 0);
                var remainingQty = shippedQty - alreadyReceived;
                
                if (incomingQty > remainingQty)
                {
                    throw new InvalidOperationException($"Cannot receive {incomingQty} units of product ID {productId}. Only {remainingQty} units remaining to receive.");
                }
                
                if (incomingQty < 1)
                {
                    throw new InvalidOperationException($"Must receive at least 1 unit of product ID {productId}.");
                }
            }

            foreach (var item in request.Items)
            {
                var location = await _db.Locations.FirstOrDefaultAsync(l => l.LocationId == item.LocationId && l.WarehouseId == request.WarehouseId);
                if (location == null)
                {
                    throw new InvalidOperationException("Selected stock-in location does not belong to the destination warehouse.");
                }

                // Validate rack capacity
                var currentStock = await _db.Inventories
                    .Where(i => i.LocationId == item.LocationId)
                    .SumAsync(i => i.Quantity ?? 0);
                var availableSpace = location.Capacity - currentStock;
                if (item.Quantity > availableSpace)
                {
                    throw new InvalidOperationException(
                        $"Rack '{location.LocationCode}' does not have enough capacity. Available: {availableSpace}, Trying to add: {item.Quantity}");
                }
            }

            // Create a new stock-in receipt each time (allow multiple partial receipts)
            var receipt = new StockInReceipt
            {
                WarehouseId = request.WarehouseId,
                ReceivedBy = userId,
                ReceivedDate = DateTime.Now,
                Reason = "Transfer In",
                TransferId = request.TransferId,
                CreatedAt = DateTime.Now,
                TotalAmount = 0
            };
            _db.StockInReceipts.Add(receipt);
            await _db.SaveChangesAsync();

            decimal totalAmount = 0;
            foreach (var item in request.Items)
            {
                _db.StockInDetails.Add(new StockInDetail
                {
                    StockInId = receipt.StockInId,
                    ProductId = item.ProductId,
                    LocationId = item.LocationId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });

                var inventory = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.LocationId == item.LocationId);
                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        ProductId = item.ProductId,
                        LocationId = item.LocationId,
                        Quantity = item.Quantity,
                        LastUpdated = DateTime.Now
                    };
                    _db.Inventories.Add(inventory);
                }
                else
                {
                    inventory.Quantity = (inventory.Quantity ?? 0) + item.Quantity;
                    inventory.LastUpdated = DateTime.Now;
                }

                totalAmount += item.Quantity * item.UnitPrice;
            }

            receipt.ReceivedBy = userId;
            receipt.ReceivedDate = DateTime.Now;
            receipt.TotalAmount = totalAmount;
            
            // Check if all shipped quantities have been received
            var totalReceivedMap = transfer.StockInReceipts
                .SelectMany(r => r.StockInDetails)
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
            bool fullyReceived = shippedMap.All(kvp => 
                totalReceivedMap.GetValueOrDefault(kvp.Key, 0) >= kvp.Value);
            
            transfer.Status = fullyReceived ? "Completed" : "In Transit";

            await _db.SaveChangesAsync();
            return receipt.StockInId;
        }

        public async Task<bool> ApproveTransferAsync(int transferId, int approvedBy, int userWarehouseId)
        {
            var transfer = await _db.TransferRequests.FindAsync(transferId);
            if (transfer == null)
            {
                throw new InvalidOperationException("Transfer request not found.");
            }

            if (transfer.Status != "Pending")
            {
                throw new InvalidOperationException($"Cannot approve transfer with status: {transfer.Status}");
            }

            if (transfer.FromWarehouseId != userWarehouseId)
            {
                throw new InvalidOperationException("Only the Warehouse Manager of the source warehouse can approve this transfer.");
            }

            transfer.Status = "Approved";
            transfer.ApprovedBy = approvedBy;
            transfer.ApprovedDate = DateTime.Now;

            _db.TransferRequests.Update(transfer);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectTransferAsync(int transferId, int rejectedBy, int userWarehouseId, string? rejectionReason = null)
        {
            var transfer = await _db.TransferRequests.FindAsync(transferId);
            if (transfer == null)
            {
                throw new InvalidOperationException("Transfer request not found.");
            }

            if (transfer.Status == "Pending Destination")
            {
                if (transfer.FromWarehouseId != userWarehouseId)
                {
                    throw new InvalidOperationException("You can only reject transfers from your warehouse.");
                }
            }
            else if (transfer.Status == "Pending")
            {
                if (transfer.ToWarehouseId != userWarehouseId)
                {
                    throw new InvalidOperationException("You can only reject transfers destined for your warehouse.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Cannot reject transfer with status: {transfer.Status}");
            }

            transfer.Status = "Rejected";
            transfer.ApprovedBy = rejectedBy;
            transfer.ApprovedDate = DateTime.Now;
            
            if (!string.IsNullOrEmpty(rejectionReason))
            {
                transfer.Reason = (transfer.Reason ?? "") + " | Rejection Reason: " + rejectionReason;
            }

            _db.TransferRequests.Update(transfer);
            await _db.SaveChangesAsync();

            return true;
        }
    }
}
