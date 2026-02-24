using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class LocationRepository : GenericRepository<Location>, ILocationRepository
    {
        public LocationRepository(EWMSContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Location>> GetByWarehouseIdAsync(int warehouseId)
        {
            return await _dbSet
                .Where(l => l.WarehouseId == warehouseId)
                .OrderBy(l => l.LocationCode)
                .ToListAsync();
        }

        public async Task<IEnumerable<Location>> GetByRackAsync(int warehouseId, string rack)
        {
            return await _dbSet
                .Where(l => l.WarehouseId == warehouseId && l.Rack == rack)
                .OrderBy(l => l.LocationCode)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetRacksAsync(int warehouseId)
        {
            return await _dbSet
                .Where(l => l.WarehouseId == warehouseId && l.Rack != null)
                .Select(l => l.Rack!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
        }
    }
}
