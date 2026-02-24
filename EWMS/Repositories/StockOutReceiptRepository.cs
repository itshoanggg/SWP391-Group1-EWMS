using EWMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class StockOutReceiptRepository : IStockOutReceiptRepository
    {
        private readonly EWMSDbContext _context;

        public StockOutReceiptRepository(EWMSDbContext context)
        {
            _context = context;
        }

        public async Task<List<StockOutReceipt>> GetStockOutReceiptsByWarehouseAsync(int warehouseId)
        {
            return await _context.StockOutReceipts
                .Include(s => s.Warehouse)
                .Include(s => s.IssuedByNavigation)
                .Include(s => s.SalesOrder)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Product)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Location)
                .Where(s => s.WarehouseId == warehouseId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<StockOutReceipt?> GetStockOutReceiptByIdAsync(int stockOutId)
        {
            return await _context.StockOutReceipts
                .Include(s => s.Warehouse)
                .Include(s => s.IssuedByNavigation)
                .Include(s => s.SalesOrder)
                    .ThenInclude(so => so!.SalesOrderDetails)
                        .ThenInclude(d => d.Product)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Product)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Location)
                .FirstOrDefaultAsync(s => s.StockOutId == stockOutId);
        }

        public async Task<StockOutReceipt?> GetStockOutReceiptBySalesOrderIdAsync(int salesOrderId)
        {
            return await _context.StockOutReceipts
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Product)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Location)
                .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        }

        public async Task<int> GetShippedQuantityAsync(int salesOrderId, int productId)
        {
            return await _context.StockOutDetails
                .Where(d => d.StockOut.SalesOrderId == salesOrderId && d.ProductId == productId)
                .SumAsync(d => (int?)d.Quantity ?? 0);
        }

        public async Task<StockOutReceipt> CreateStockOutReceiptAsync(StockOutReceipt stockOutReceipt)
        {
            _context.StockOutReceipts.Add(stockOutReceipt);
            await _context.SaveChangesAsync();
            return stockOutReceipt;
        }

        public async Task<bool> UpdateInventoryAsync(int productId, int locationId, int quantity)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);

            if (inventory == null)
            {
                return false;
            }

            if (inventory.Quantity < quantity)
            {
                return false;
            }

            inventory.Quantity -= quantity;
            inventory.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
