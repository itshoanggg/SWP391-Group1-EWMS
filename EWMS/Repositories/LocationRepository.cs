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

        // Legacy methods used by StockService via UnitOfWork (Interfaces namespace)
        public async Task<IEnumerable<Location>> GetByWarehouseIdAsync(int warehouseId)
        {
            return await _dbSet
                .Include(l => l.Inventories)
                .Where(l => l.WarehouseId == warehouseId)
                .OrderBy(l => l.LocationCode)
                .ToListAsync();
        }

        public async Task<IEnumerable<Location>> GetByRackAsync(int warehouseId, string rack)
        {
            return await _dbSet
                .Include(l => l.Inventories)
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

        // Stock-Out/Sales/WarehouseManager methods (EWMS.Repositories namespace)
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

        // Warehouse Manager specific methods
        public async Task<(List<Location> Locations, int TotalCount)> GetLocationsPagedAsync(
            int page,
            int pageSize,
            string? searchQuery,
            int? warehouseId)
        {
            var query = _context.Locations
                .Include(l => l.Warehouse)
                .Include(l => l.Inventories)
                .AsQueryable();

            if (warehouseId.HasValue && warehouseId > 0)
            {
                query = query.Where(l => l.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                searchQuery = searchQuery.ToLower();
                query = query.Where(l =>
                    l.LocationCode.ToLower().Contains(searchQuery) ||
                    (l.LocationName != null && l.LocationName.ToLower().Contains(searchQuery)));
            }

            var totalCount = await query.CountAsync();

            var locations = await query
                .OrderBy(l => l.LocationCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (locations, totalCount);
        }

        public async Task<Location?> GetLocationWithInventoryAsync(int locationId)
        {
            return await _context.Locations
                .Include(l => l.Warehouse)
                .Include(l => l.Inventories)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(l => l.LocationId == locationId);
        }

        public async Task<bool> LocationCodeExistsAsync(string locationCode, int warehouseId, int? excludeLocationId = null)
        {
            var query = _context.Locations
                .Where(l => l.LocationCode == locationCode && l.WarehouseId == warehouseId);

            if (excludeLocationId.HasValue)
            {
                query = query.Where(l => l.LocationId != excludeLocationId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<int> GetLocationUsedCapacityAsync(int locationId)
        {
            var inventories = await _context.Inventories
                .Where(i => i.LocationId == locationId)
                .ToListAsync();

            return inventories.Sum(i => i.Quantity ?? 0);
        }
    }
}
