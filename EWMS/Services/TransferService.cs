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
            request.RequestedBy = requestedBy;
            request.RequestedDate = DateTime.Now;
            _db.TransferRequests.Add(request);
            await _db.SaveChangesAsync();

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
    }
}