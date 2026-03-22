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

        public async Task<(List<Warehouse> Warehouses, int TotalCount)> GetWarehousesPagedAsync(
            int page, 
            int pageSize, 
            string? searchQuery)
        {
            var query = _context.Warehouses
                .Include(w => w.Locations)
                    .ThenInclude(l => l.Inventories)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                searchQuery = searchQuery.ToLower();
                query = query.Where(w => 
                    w.WarehouseName.ToLower().Contains(searchQuery) ||
                    (w.Address != null && w.Address.ToLower().Contains(searchQuery)));
            }

            var totalCount = await query.CountAsync();

            var warehouses = await query
                .OrderBy(w => w.WarehouseId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (warehouses, totalCount);
        }

        public async Task<Warehouse?> GetWarehouseWithLocationsAsync(int warehouseId)
        {
            return await _context.Warehouses
                .Include(w => w.Locations)
                    .ThenInclude(l => l.Inventories)
                .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId);
        }

        public async Task<bool> WarehouseExistsAsync(int warehouseId)
        {
            return await _context.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId);
        }

        public async Task<List<Warehouse>> GetAllWarehousesAsync()
        {
            return await _context.Warehouses
                .OrderBy(w => w.WarehouseName)
                .ToListAsync();
        }
    }
}
