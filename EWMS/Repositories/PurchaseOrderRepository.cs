using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class PurchaseOrderRepository : GenericRepository<PurchaseOrder>, IPurchaseOrderRepository
    {
        public PurchaseOrderRepository(EWMSDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PurchaseOrder>> GetByWarehouseIdAsync(int warehouseId, string? status = null)
        {
            var query = _dbSet
                .Include(po => po.Supplier)
                .Include(po => po.CreatedByNavigation)
                .Include(po => po.PurchaseOrderDetails)
                .Where(po => po.WarehouseId == warehouseId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(po => po.Status == status);
            }

            return await query.OrderByDescending(po => po.CreatedAt).ToListAsync();
        }

        public async Task<PurchaseOrder?> GetByIdWithDetailsAsync(int id, int warehouseId)
        {
            return await _dbSet
                .Include(po => po.Supplier)
                .Include(po => po.Warehouse)
                .Include(po => po.CreatedByNavigation)
                .Include(po => po.PurchaseOrderDetails)
                    .ThenInclude(pod => pod.Product)
                        .ThenInclude(p => p.Category)
                .Include(po => po.StockInReceipts)
                    .ThenInclude(si => si.StockInDetails)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);
        }

        public async Task UpdateToReadyToReceiveAsync(int warehouseId)
        {
            var today = DateTime.Today;

            var ordersToUpdate = await _dbSet
                .Where(po =>
                    po.WarehouseId == warehouseId &&
                    po.Status == "Ordered" &&
                    po.CreatedAt.Date <= today)
                .ToListAsync();

            foreach (var po in ordersToUpdate)
            {
                po.Status = "ReadyToReceive";
            }
        }
    }
}
