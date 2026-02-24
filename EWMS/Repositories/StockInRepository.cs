using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class StockInRepository : GenericRepository<StockInReceipt>, IStockInRepository
    {
        public StockInRepository(EWMSContext context) : base(context)
        {
        }

        public async Task<IEnumerable<StockInReceipt>> GetByWarehouseIdAsync(int warehouseId)
        {
            return await _dbSet
                .Include(si => si.StockInDetails)
                .Where(si => si.WarehouseId == warehouseId)
                .OrderByDescending(si => si.ReceivedDate)
                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> GetReceivedQuantitiesAsync(int purchaseOrderId)
        {
            return await _dbSet
                .Where(si => si.PurchaseOrderId == purchaseOrderId)
                .SelectMany(si => si.StockInDetails)
                .GroupBy(sid => sid.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalReceived = g.Sum(sid => sid.Quantity)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.TotalReceived);
        }
    }
}
