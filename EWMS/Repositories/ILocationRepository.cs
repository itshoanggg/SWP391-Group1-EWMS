using EWMS.DTOs;
using EWMS.Models;

namespace EWMS.Repositories
{
    public interface ILocationRepository
    {
        // Stock-Out/Sales specific methods
        Task<List<Location>> GetLocationsByWarehouseAsync(int warehouseId);
        Task<Location?> GetLocationByIdAsync(int locationId);
        Task<List<LocationInventoryDto>> GetLocationInventoryByProductAsync(int warehouseId, int productId);
        Task<int> GetAvailableQuantityAsync(int productId, int locationId);
        
        // Warehouse Manager specific methods
        Task<(List<Location> Locations, int TotalCount)> GetLocationsPagedAsync(int page, int pageSize, string? searchQuery, int? warehouseId);
        Task<Location?> GetLocationWithInventoryAsync(int locationId);
        Task<bool> LocationCodeExistsAsync(string locationCode, int warehouseId, int? excludeLocationId = null);
        Task<int> GetLocationUsedCapacityAsync(int locationId);
    }
}
