using EWMS.DTOs;
using EWMS.Models;

namespace EWMS.Repositories
{
    public interface ILocationRepository
    {
        Task<List<Location>> GetLocationsByWarehouseAsync(int warehouseId);
        Task<Location?> GetLocationByIdAsync(int locationId);
        Task<List<LocationInventoryDto>> GetLocationInventoryByProductAsync(int warehouseId, int productId);
        Task<int> GetAvailableQuantityAsync(int productId, int locationId);
    }
}
