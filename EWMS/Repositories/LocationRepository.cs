using EWMS.DTOs;
using EWMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        private readonly EWMSDbContext _context;

        public LocationRepository(EWMSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Location>> GetLocationsByWarehouseAsync(int warehouseId)
        {
            return await _context.Locations
                .Where(l => l.WarehouseId == warehouseId)
                .OrderBy(l => l.LocationCode)
                .ToListAsync();
        }

        public async Task<Location?> GetLocationByIdAsync(int locationId)
        {
            return await _context.Locations
                .Include(l => l.Warehouse)
                .FirstOrDefaultAsync(l => l.LocationId == locationId);
        }

        public async Task<List<LocationInventoryDto>> GetLocationInventoryByProductAsync(int warehouseId, int productId)
        {
            var result = await _context.Inventories
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId && i.ProductId == productId && i.Quantity > 0)
                .Select(i => new LocationInventoryDto
                {
                    LocationId = i.LocationId,
                    LocationCode = i.Location.LocationCode,
                    LocationName = i.Location.LocationName,
                    ProductId = i.ProductId,
                    AvailableQuantity = i.Quantity ?? 0
                })
                .OrderBy(l => l.LocationCode)
                .ToListAsync();

            return result;
        }

        public async Task<int> GetAvailableQuantityAsync(int productId, int locationId)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);

            return inventory?.Quantity ?? 0;
        }
    }
}
