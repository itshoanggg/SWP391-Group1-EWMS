using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class LocationRepository : GenericRepository<Location>,
        EWMS.Repositories.Interfaces.ILocationRepository,
        EWMS.Repositories.ILocationRepository
    {
        public LocationRepository(EWMSDbContext context) : base(context)
        {
        }

        // Legacy (Interfaces) methods used by StockService via UnitOfWork
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

        // New (master) methods used by StockOutReceiptService, etc.
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
                .Where(i => i.Location.WarehouseId == warehouseId && i.ProductId == productId && (i.Quantity ?? 0) > 0)
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
