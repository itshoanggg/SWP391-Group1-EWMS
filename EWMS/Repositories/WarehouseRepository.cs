using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class WarehouseRepository : GenericRepository<Warehouse>, IWarehouseRepository
    {
        public WarehouseRepository(EWMSDbContext context) : base(context)
        {
        }

        public async Task<string?> GetWarehouseNameByIdAsync(int warehouseId)
        {
            var warehouse = await _context.Set<Warehouse>()
                .Where(w => w.WarehouseId == warehouseId)
                .Select(w => w.WarehouseName)
                .FirstOrDefaultAsync();
            
            return warehouse;
        }
    }
}
