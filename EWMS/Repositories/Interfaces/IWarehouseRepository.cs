using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IWarehouseRepository : IGenericRepository<Warehouse>
    {
        Task<string?> GetWarehouseNameByIdAsync(int warehouseId);
        Task<(List<Warehouse> Warehouses, int TotalCount)> GetWarehousesPagedAsync(int page, int pageSize, string? searchQuery);
        Task<Warehouse?> GetWarehouseWithLocationsAsync(int warehouseId);
        Task<bool> WarehouseExistsAsync(int warehouseId);
        Task<List<Warehouse>> GetAllWarehousesAsync();
    }
}
