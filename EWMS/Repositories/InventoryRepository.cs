using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class InventoryRepository : GenericRepository<Inventory>, IInventoryRepository
    {
        public InventoryRepository(EWMSContext context) : base(context)
        {
        }

        public async Task<Inventory?> GetByProductAndLocationAsync(int productId, int locationId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);
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
                .Where(i => i.Location.WarehouseId == warehouseId && i.Quantity <= threshold)
                .OrderBy(i => i.Quantity)
                .ToListAsync();
        }
    }
}
