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