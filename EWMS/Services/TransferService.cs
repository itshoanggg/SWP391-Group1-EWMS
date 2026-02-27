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
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
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

        public async Task<TransferRequest?> GetTransferByIdAsync(int id)
        {
            return await _db.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .Include(t => t.TransferDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TransferId == id);
        }

        public async Task<int> CreateTransferAsync(TransferRequest request, int productId, int quantity, int requestedBy)
        {
            // Validate that product exists
            var product = await _db.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException("Product not found.");
            }

            // Check inventory availability at source warehouse
            var totalAvailable = await _db.Inventories
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId && i.Location.WarehouseId == request.FromWarehouseId)
                .SumAsync(i => i.Quantity ?? 0);

            if (totalAvailable < quantity)
            {
                throw new InvalidOperationException($"Insufficient inventory. Available: {totalAvailable}, Requested: {quantity}");
            }

            // Create transfer request
            request.RequestedBy = requestedBy;
            request.RequestedDate = DateTime.Now;
            request.Status = "Pending"; // Set default status
            _db.TransferRequests.Add(request);
            await _db.SaveChangesAsync();

            // Create transfer detail
            var detail = new TransferDetail
            {
                TransferId = request.TransferId,
                ProductId = productId,
                Quantity = quantity
            };

            _db.TransferDetails.Add(detail);
            await _db.SaveChangesAsync();

            return request.TransferId;
        }

        public async Task<bool> ApproveTransferAsync(int transferId, int approvedBy)
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

            transfer.Status = "Approved";
            transfer.ApprovedBy = approvedBy;
            transfer.ApprovedDate = DateTime.Now;

            _db.TransferRequests.Update(transfer);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RejectTransferAsync(int transferId, int rejectedBy, string? rejectionReason = null)
        {
            var transfer = await _db.TransferRequests.FindAsync(transferId);
            if (transfer == null)
            {
                throw new InvalidOperationException("Transfer request not found.");
            }

            if (transfer.Status != "Pending")
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