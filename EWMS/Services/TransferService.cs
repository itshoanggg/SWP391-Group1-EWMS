using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;

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
                .ToListAsync();
        }

        public async Task<List<TransferRequest>> GetIncomingTransfersAsync(int warehouseId)
        {
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .Include(t => t.ApprovedByNavigation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Where(t => t.ToWarehouseId == warehouseId)
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

        public async Task<List<Location>> GetLocationsByWarehouseAsync(int warehouseId)
        {
            return await _db.Locations
                .Where(l => l.WarehouseId == warehouseId)
                .ToListAsync();
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

        public async Task<TransferRequest?> GetTransferByIdAsync(int id)
        {
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .Include(t => t.ApprovedByNavigation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TransferId == id);
        }

        public async Task<int> CreateTransferAsync(TransferRequest request, int productId, int quantity, int requestedBy, int fromLocationId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException("Product not found.");
            }

            if (!string.IsNullOrEmpty(request.FromRack))
            {
                var rackExists = await _db.Locations
                    .AnyAsync(l => l.WarehouseId == request.FromWarehouseId && l.Rack == request.FromRack);
                
                if (!rackExists)
                {
                    throw new InvalidOperationException($"Rack '{request.FromRack}' not found in source warehouse.");
                }

                var availableInRack = await _db.Inventories
                    .Include(i => i.Location)
                    .Where(i => i.ProductId == productId && i.Location.WarehouseId == request.FromWarehouseId && i.Location.Rack == request.FromRack)
                    .SumAsync(i => i.Quantity ?? 0);

                if (availableInRack < quantity)
                {
                    throw new InvalidOperationException($"Insufficient inventory in rack {request.FromRack}. Available: {availableInRack}, Requested: {quantity}");
                }
            }
            else
            {
                var totalAvailable = await _db.Inventories
                    .Include(i => i.Location)
                    .Where(i => i.ProductId == productId && i.Location.WarehouseId == request.FromWarehouseId)
                    .SumAsync(i => i.Quantity ?? 0);

                if (totalAvailable < quantity)
                {
                    throw new InvalidOperationException($"Insufficient inventory. Available: {totalAvailable}, Requested: {quantity}");
                }
            }

            request.RequestedBy = requestedBy;
            request.RequestedDate = DateTime.Now;
            request.Status = "Pending Destination";
            request.TransferType = "Transfer";
            _db.TransferRequests.Add(request);
            await _db.SaveChangesAsync();

            var detail = new TransferDetail
            {
                TransferId = request.TransferId,
                ProductId = productId,
                Quantity = quantity,
                FromLocationId = fromLocationId
            };

            _db.TransferDetails.Add(detail);
            await _db.SaveChangesAsync();

            return request.TransferId;
        }

        public async Task<bool> ApproveTransferAsync(int transferId, int approvedBy, int userWarehouseId, bool isAdmin = false, int? toWarehouseId = null, int? toLocationId = null)
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
                    throw new InvalidOperationException("You can only process transfers from your warehouse.");
                }

                if (!toWarehouseId.HasValue)
                {
                    throw new InvalidOperationException("Please select a destination warehouse.");
                }

                if (toWarehouseId.Value == transfer.FromWarehouseId)
                {
                    throw new InvalidOperationException("Destination warehouse must be different from source warehouse.");
                }

                transfer.ToWarehouseId = toWarehouseId.Value;
                transfer.Status = "Pending";
                transfer.ApprovedBy = approvedBy;
                transfer.ApprovedDate = DateTime.Now;

                // Save destination location to each transfer detail if provided
                if (toLocationId.HasValue)
                {
                    var details = await _db.TransferDetails
                        .Where(td => td.TransferId == transferId)
                        .ToListAsync();
                    foreach (var td in details)
                    {
                        td.ToLocationId = toLocationId.Value;
                    }
                }
            }
            else if (transfer.Status == "Pending")
            {
                if (transfer.ToWarehouseId != userWarehouseId)
                {
                    throw new InvalidOperationException("You can only approve transfers destined for your warehouse.");
                }

                transfer.Status = "Approved";
                transfer.ApprovedBy = approvedBy;
                transfer.ApprovedDate = DateTime.Now;

                var transferDetails = await _db.TransferDetails
                    .Where(td => td.TransferId == transferId)
                    .ToListAsync();

                var stockOutReceipt = new StockOutReceipt
                {
                    WarehouseId = transfer.FromWarehouseId,
                    IssuedBy = approvedBy,
                    IssuedDate = DateTime.Now,
                    Reason = "Transfer Out",
                    TransferId = transferId,
                    CreatedAt = DateTime.Now
                };
                _db.StockOutReceipts.Add(stockOutReceipt);
                await _db.SaveChangesAsync();

                foreach (var td in transferDetails)
                {
                    var stockOutDetail = new StockOutDetail
                    {
                        StockOutId = stockOutReceipt.StockOutId,
                        ProductId = td.ProductId,
                        Quantity = td.Quantity
                    };
                    _db.StockOutDetails.Add(stockOutDetail);

                    // Deduct from the specific source location
                    var inventory = td.FromLocationId.HasValue
                        ? await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == td.ProductId && i.LocationId == td.FromLocationId.Value)
                        : await _db.Inventories
                            .Include(i => i.Location)
                            .FirstOrDefaultAsync(i => i.ProductId == td.ProductId && i.Location.WarehouseId == transfer.FromWarehouseId);

                    if (inventory != null && (inventory.Quantity ?? 0) >= td.Quantity)
                    {
                        inventory.Quantity -= td.Quantity;
                    }
                }

                var stockInReceipt = new StockInReceipt
                {
                    WarehouseId = transfer.ToWarehouseId.Value,
                    ReceivedBy = approvedBy,
                    ReceivedDate = DateTime.Now,
                    Reason = "Transfer In",
                    TransferId = transferId,
                    CreatedAt = DateTime.Now
                };
                _db.StockInReceipts.Add(stockInReceipt);
                await _db.SaveChangesAsync();

                foreach (var td in transferDetails)
                {
                    var stockInDetail = new StockInDetail
                    {
                        StockInId = stockInReceipt.StockInId,
                        ProductId = td.ProductId,
                        Quantity = td.Quantity
                    };
                    _db.StockInDetails.Add(stockInDetail);

                    // Add to the specific destination location if saved, otherwise find existing
                    int? destLocationId = td.ToLocationId;

                    if (destLocationId.HasValue)
                    {
                        var destInventory = await _db.Inventories
                            .FirstOrDefaultAsync(i => i.ProductId == td.ProductId && i.LocationId == destLocationId.Value);

                        if (destInventory != null)
                        {
                            destInventory.Quantity = (destInventory.Quantity ?? 0) + td.Quantity;
                        }
                        else
                        {
                            _db.Inventories.Add(new Inventory
                            {
                                ProductId = td.ProductId,
                                LocationId = destLocationId.Value,
                                Quantity = td.Quantity
                            });
                        }
                    }
                    else
                    {
                        var existingInventory = await _db.Inventories
                            .Include(i => i.Location)
                            .FirstOrDefaultAsync(i => i.ProductId == td.ProductId && i.Location.WarehouseId == transfer.ToWarehouseId);

                        if (existingInventory != null)
                        {
                            existingInventory.Quantity = (existingInventory.Quantity ?? 0) + td.Quantity;
                        }
                        else
                        {
                            var location = await _db.Locations
                                .FirstOrDefaultAsync(l => l.WarehouseId == transfer.ToWarehouseId);

                            if (location != null)
                            {
                                _db.Inventories.Add(new Inventory
                                {
                                    ProductId = td.ProductId,
                                    LocationId = location.LocationId,
                                    Quantity = td.Quantity
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"Cannot approve transfer with status: {transfer.Status}");
            }

        public async Task<bool> ApproveTransferAsync(int transferId, int approvedBy, int userWarehouseId, int toWarehouseId)
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

        public async Task<bool> UpdateToRackAsync(int transferId, string toRack, int userId)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var transfer = await _db.TransferRequests
                    .Include(t => t.TransferDetails)
                    .FirstOrDefaultAsync(t => t.TransferId == transferId);
                    
                if (transfer == null)
                {
                    throw new InvalidOperationException("Transfer request not found.");
                }

                if (transfer.Status != "Approved")
                {
                    throw new InvalidOperationException("Can only set destination rack for approved transfers.");
                }

                var toLocation = await _db.Locations
                    .FirstOrDefaultAsync(l => l.WarehouseId == transfer.ToWarehouseId && l.Rack == toRack);
                    
                if (toLocation == null)
                {
                    throw new InvalidOperationException($"Rack '{toRack}' not found in destination warehouse.");
                }

                var fromLocation = await _db.Locations
                    .FirstOrDefaultAsync(l => l.WarehouseId == transfer.FromWarehouseId && l.Rack == transfer.FromRack);
                    
                if (fromLocation == null)
                {
                    throw new InvalidOperationException($"Rack '{transfer.FromRack}' not found in source warehouse.");
                }

                transfer.ToRack = toRack;
                _db.TransferRequests.Update(transfer);

                var stockOutReceipt = new StockOutReceipt
                {
                    WarehouseId = transfer.FromWarehouseId,
                    IssuedBy = userId,
                    IssuedDate = DateTime.Now,
                    Reason = "Transfer",
                    TransferId = transferId,
                    TotalAmount = 0,
                    CreatedAt = DateTime.Now
                };
                _db.StockOutReceipts.Add(stockOutReceipt);
                await _db.SaveChangesAsync();

                var stockInReceipt = new StockInReceipt
                {
                    WarehouseId = transfer.ToWarehouseId,
                    ReceivedBy = userId,
                    ReceivedDate = DateTime.Now,
                    Reason = "Transfer",
                    TransferId = transferId,
                    TotalAmount = 0,
                    CreatedAt = DateTime.Now
                };
                _db.StockInReceipts.Add(stockInReceipt);
                await _db.SaveChangesAsync();

                foreach (var detail in transfer.TransferDetails)
                {
                    var product = await _db.Products.FindAsync(detail.ProductId);
                    var unitPrice = product?.SellingPrice ?? 0;

                    var stockOutDetail = new StockOutDetail
                    {
                        StockOutId = stockOutReceipt.StockOutId,
                        ProductId = detail.ProductId,
                        LocationId = fromLocation.LocationId,
                        Quantity = detail.Quantity,
                        UnitPrice = unitPrice
                    };
                    _db.StockOutDetails.Add(stockOutDetail);

                    var fromInventory = await _db.Inventories
                        .FirstOrDefaultAsync(i => i.LocationId == fromLocation.LocationId && i.ProductId == detail.ProductId);
                        
                    if (fromInventory != null && fromInventory.Quantity.HasValue)
                    {
                        fromInventory.Quantity -= detail.Quantity;
                        if (fromInventory.Quantity < 0) fromInventory.Quantity = 0;
                        _db.Inventories.Update(fromInventory);
                    }

                    var stockInDetail = new StockInDetail
                    {
                        StockInId = stockInReceipt.StockInId,
                        ProductId = detail.ProductId,
                        LocationId = toLocation.LocationId,
                        Quantity = detail.Quantity,
                        UnitPrice = unitPrice
                    };
                    _db.StockInDetails.Add(stockInDetail);

                    var toInventory = await _db.Inventories
                        .FirstOrDefaultAsync(i => i.LocationId == toLocation.LocationId && i.ProductId == detail.ProductId);
                        
                    if (toInventory != null && toInventory.Quantity.HasValue)
                    {
                        toInventory.Quantity += detail.Quantity;
                        _db.Inventories.Update(toInventory);
                    }
                    else
                    {
                        var newInventory = new Inventory
                        {
                            LocationId = toLocation.LocationId,
                            ProductId = detail.ProductId,
                            Quantity = detail.Quantity
                        };
                        _db.Inventories.Add(newInventory);
                    }
                }

                transfer.Status = "Completed";
                _db.TransferRequests.Update(transfer);
                
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}