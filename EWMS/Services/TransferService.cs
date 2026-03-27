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

                // Create TransferRequest
                var transfer = new TransferRequest
                {
                    FromWarehouseId = model.FromWarehouseId,
                    ToWarehouseId = model.ToWarehouseId,
                    TransferType = "Transfer",
                    RequestedBy = userId,
                    RequestedDate = DateTime.Now,
                    ApprovedBy = userId,
                    ApprovedDate = DateTime.Now,
                    Status = "Completed",
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

                // Create StockOutReceipt
                var stockOutReceipt = new StockOutReceipt
                {
                    WarehouseId = model.FromWarehouseId,
                    IssuedBy = userId,
                    IssuedDate = DateTime.Now,
                    Reason = "Transfer Out",
                    TransferId = transfer.TransferId,
                    CreatedAt = DateTime.Now,
                    TotalAmount = 0
                };
                _db.StockOutReceipts.Add(stockOutReceipt);
                await _db.SaveChangesAsync();

                // Create StockInReceipt
                var stockInReceipt = new StockInReceipt
                {
                    WarehouseId = model.ToWarehouseId,
                    ReceivedBy = userId,
                    ReceivedDate = DateTime.Now,
                    Reason = "Transfer In",
                    TransferId = transfer.TransferId,
                    CreatedAt = DateTime.Now,
                    TotalAmount = 0
                };
                _db.StockInReceipts.Add(stockInReceipt);
                await _db.SaveChangesAsync();

                decimal totalOutAmount = 0;
                decimal totalInAmount = 0;

                foreach (var item in model.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    var unitPrice = product?.SellingPrice ?? 0;

                    // Create StockOutDetail
                    _db.StockOutDetails.Add(new StockOutDetail
                    {
                        StockOutId = stockOutReceipt.StockOutId,
                        ProductId = item.ProductId,
                        LocationId = item.FromLocationId,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice
                    });

                    // Deduct from source inventory
                    var sourceInv = await _db.Inventories
                        .FirstAsync(i => i.ProductId == item.ProductId && i.LocationId == item.FromLocationId);
                    sourceInv.Quantity = (sourceInv.Quantity ?? 0) - item.Quantity;
                    sourceInv.LastUpdated = DateTime.Now;

                    totalOutAmount += item.Quantity * unitPrice;

                    // Create StockInDetail
                    _db.StockInDetails.Add(new StockInDetail
                    {
                        StockInId = stockInReceipt.StockInId,
                        ProductId = item.ProductId,
                        LocationId = item.ToLocationId,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice
                    });

                    // Add to destination inventory
                    var destInv = await _db.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.LocationId == item.ToLocationId);
                    if (destInv == null)
                    {
                        destInv = new Inventory
                        {
                            ProductId = item.ProductId,
                            LocationId = item.ToLocationId,
                            Quantity = item.Quantity,
                            LastUpdated = DateTime.Now
                        };
                        _db.Inventories.Add(destInv);
                    }
                    else
                    {
                        destInv.Quantity = (destInv.Quantity ?? 0) + item.Quantity;
                        destInv.LastUpdated = DateTime.Now;
                    }

                    totalInAmount += item.Quantity * unitPrice;
                }

                stockOutReceipt.TotalAmount = totalOutAmount;
                stockInReceipt.TotalAmount = totalInAmount;

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
                            (t.Status == "Approved" || t.Status == "In Transit" || t.Status == "Completed"))
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
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
                .Include(t => t.StockInReceipts)
                    .ThenInclude(r => r.StockInDetails)
                .Where(t => t.ToWarehouseId == warehouseId &&
                            (t.Status == "Approved" || t.Status == "In Transit" || t.Status == "Completed"))
                .OrderByDescending(t => t.RequestedDate)
                .Select(t => new PendingTransferReceiptViewModel
                {
                    TransferId = t.TransferId,
                    WarehouseId = t.ToWarehouseId ?? 0,
                    WarehouseName = t.ToWarehouse.WarehouseName,
                    CounterpartyWarehouseName = t.FromWarehouse.WarehouseName,
                    ProductSummary = string.Join(", ", t.TransferDetails.Select(d => d.Product.ProductName)),
                    TotalQuantity = t.TransferDetails.Sum(d => d.Quantity),
                    RequestedDate = t.RequestedDate,
                    Status = t.Status ?? string.Empty,
                    IsProcessed = t.StockInReceipts.Any(r => r.StockInDetails.Any())
                })
                .ToListAsync();
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

        public async Task<TransferStockInPageViewModel?> GetTransferStockInAsync(int transferId, int warehouseId)
        {
            var transfer = await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
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

            return new TransferStockInPageViewModel
            {
                TransferId = transfer.TransferId,
                WarehouseId = warehouseId,
                WarehouseName = transfer.ToWarehouse?.WarehouseName ?? "Unknown",
                SourceWarehouseName = transfer.FromWarehouse.WarehouseName,
                Reason = transfer.Reason,
                Details = stockOutDetails.Select(d => new TransferStockInItemViewModel
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product.ProductName,
                    Quantity = d.Quantity,
                    Unit = d.Product.Unit ?? string.Empty,
                    UnitPrice = d.UnitPrice
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

            if (transfer.Status != "Approved" && transfer.Status != "In Transit")
            {
                throw new InvalidOperationException($"Cannot process stock-out for transfer status: {transfer.Status}");
            }

            var expectedMap = transfer.TransferDetails.ToDictionary(x => x.ProductId, x => x.Quantity);
            foreach (var detail in model.Details)
            {
                if (!expectedMap.TryGetValue(detail.ProductId, out var expectedQty) || expectedQty != detail.Quantity)
                {
                    throw new InvalidOperationException("Transfer stock-out quantities do not match the transfer request.");
                }

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
            transfer.Status = "In Transit";

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

            if (transfer.Status != "Approved" && transfer.Status != "In Transit")
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

            var expectedMap = stockOutDetails.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var actualMap = request.Items.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            if (expectedMap.Count != actualMap.Count || expectedMap.Any(x => !actualMap.TryGetValue(x.Key, out var qty) || qty != x.Value))
            {
                throw new InvalidOperationException("Transfer stock-in quantities must match the processed stock-out.");
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

            var receipt = await _db.StockInReceipts
                .Include(r => r.StockInDetails)
                .FirstOrDefaultAsync(r => r.TransferId == request.TransferId);

            if (receipt == null)
            {
                receipt = new StockInReceipt
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
            }
            else if (receipt.StockInDetails.Any())
            {
                throw new InvalidOperationException("Transfer stock-in has already been processed.");
            }

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
            transfer.Status = "Completed";

            await _db.SaveChangesAsync();
            return receipt.StockInId;
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
