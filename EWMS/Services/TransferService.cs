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

        public async Task<int> CreateTransferAsync(TransferRequest request, int productId, int quantity, int requestedBy, int? fromLocationId = null)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException("Product not found.");
            }

            var totalAvailable = await _db.Inventories
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId && i.Location.WarehouseId == request.FromWarehouseId)
                .SumAsync(i => i.Quantity ?? 0);

            if (totalAvailable < quantity)
            {
                throw new InvalidOperationException($"Insufficient inventory. Available: {totalAvailable}, Requested: {quantity}");
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
            var transfer = await _db.TransferRequests
                .Include(t => t.TransferDetails)
                .FirstOrDefaultAsync(t => t.TransferId == transferId);
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
                
                var existingStockOut = await _db.StockOutReceipts
                    .FirstOrDefaultAsync(x => x.TransferId == transferId);
                if (existingStockOut == null)
                {
                    var stockOutReceipt = new StockOutReceipt
                    {
                        WarehouseId = transfer.FromWarehouseId,
                        IssuedBy = approvedBy,
                        IssuedDate = DateTime.Now,
                        Reason = "Transfer Out",
                        TransferId = transferId,
                        CreatedAt = DateTime.Now,
                        TotalAmount = 0
                    };
                    _db.StockOutReceipts.Add(stockOutReceipt);
                }

                var existingStockIn = await _db.StockInReceipts
                    .FirstOrDefaultAsync(x => x.TransferId == transferId);
                if (existingStockIn == null)
                {
                    var stockInReceipt = new StockInReceipt
                    {
                        WarehouseId = transfer.ToWarehouseId.Value,
                        ReceivedBy = approvedBy,
                        ReceivedDate = DateTime.Now,
                        Reason = "Transfer In",
                        TransferId = transferId,
                        CreatedAt = DateTime.Now,
                        TotalAmount = 0
                    };
                    _db.StockInReceipts.Add(stockInReceipt);
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

        public async Task<List<PendingTransferReceiptViewModel>> GetPendingTransferStockOutAsync(int warehouseId)
        {
            return await _db.TransferRequests
                .Include(t => t.ToWarehouse)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .Include(t => t.StockOutReceipts)
                    .ThenInclude(r => r.StockOutDetails)
                .Where(t => t.FromWarehouseId == warehouseId && (t.Status == "Approved" || t.Status == "In Transit"))
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
                .Where(t => t.ToWarehouseId == warehouseId && (t.Status == "Approved" || t.Status == "In Transit"))
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
