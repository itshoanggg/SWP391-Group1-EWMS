using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    // Implement both interface sets so legacy UoW features and new services both work
    public class InventoryRepository : GenericRepository<Inventory>,
        EWMS.Repositories.Interfaces.IInventoryRepository,
        EWMS.Repositories.IInventoryRepository
    {
        public InventoryRepository(EWMSDbContext context) : base(context)
        {
        }

        // Legacy (Interfaces) methods
        public async Task<Inventory?> GetByProductAndLocationAsync(int productId, int locationId)
        {
            return await _dbSet.FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);
        }

        public async Task<IEnumerable<Inventory>> GetByWarehouseIdAsync(int warehouseId)
        {
            return await _dbSet
                .Include(i => i.Product)
                    .ThenInclude(p => p.Category)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Inventory>> GetLowStockAsync(int warehouseId, int threshold)
        {
            return await _dbSet
                .Include(i => i.Product)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId && (i.Quantity ?? 0) <= threshold)
                .OrderBy(i => i.Quantity)
                .ToListAsync();
        }

        // New (master) methods used by InventoryCheck and Sales flows
        public async Task<int> GetCurrentStockAsync(int productId, int warehouseId)
        {
            var totalStock = await _context.Inventories
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId && i.Location.WarehouseId == warehouseId)
                .SumAsync(i => i.Quantity ?? 0);
            return totalStock;
        }

        public async Task<int> GetExpectedIncomingAsync(int productId, int warehouseId, DateTime beforeDate)
        {
            var expectedFromPurchase = await _context.PurchaseOrderDetails
                .Include(pod => pod.PurchaseOrder)
                .Where(pod => pod.ProductId == productId
                    && pod.PurchaseOrder.WarehouseId == warehouseId
                    && pod.PurchaseOrder.Status == "Approved"
                    && pod.PurchaseOrder.ExpectedReceivingDate < beforeDate
                    && !_context.StockInReceipts.Any(sir => sir.PurchaseOrderId == pod.PurchaseOrderId))
                .SumAsync(pod => pod.Quantity);

            var expectedFromTransfer = await _context.TransferDetails
                .Include(td => td.Transfer)
                .Where(td => td.ProductId == productId
                    && td.Transfer.ToWarehouseId == warehouseId
                    && td.Transfer.Status == "Approved"
                    && td.Transfer.ApprovedDate < beforeDate
                    && !_context.StockInReceipts.Any(sir => sir.TransferId == td.TransferId))
                .SumAsync(td => td.Quantity);

            return expectedFromPurchase + expectedFromTransfer;
        }

        public async Task<int> GetPendingOutgoingAsync(int productId, int warehouseId)
        {
            var pendingFromSales = await _context.SalesOrderDetails
                .Include(sod => sod.SalesOrder)
                .Where(sod => sod.ProductId == productId
                    && sod.SalesOrder.WarehouseId == warehouseId
                    && sod.SalesOrder.Status == "Pending"
                    && !_context.StockOutReceipts.Any(sor => sor.SalesOrderId == sod.SalesOrderId))
                .SumAsync(sod => sod.Quantity);

            var pendingFromTransfer = await _context.TransferDetails
                .Include(td => td.Transfer)
                .Where(td => td.ProductId == productId
                    && td.Transfer.FromWarehouseId == warehouseId
                    && td.Transfer.Status == "Approved"
                    && !_context.StockOutReceipts.Any(sor => sor.TransferId == td.TransferId))
                .SumAsync(td => td.Quantity);

            return pendingFromSales + pendingFromTransfer;
        }

        public async Task<List<Inventory>> GetInventoryByWarehouseAsync(int warehouseId)
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId)
                .ToListAsync();
        }

        public async Task<Inventory?> GetInventoryByProductAndLocationAsync(int productId, int locationId)
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .Include(i => i.Location)
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);
        }
    }
}
