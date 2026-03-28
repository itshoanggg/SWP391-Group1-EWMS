using EWMS.DTOs;
using EWMS.Models;

namespace EWMS.Services.Interfaces
{
    public interface IUserService
    {
        Task<int> GetWarehouseIdByUserIdAsync(int userId);
        Task<string?> GetWarehouseNameByUserIdAsync(int userId);
        int GetCurrentUserId();
        
        // Warehouse access methods
        Task<List<Warehouse>> GetWarehousesForUserAsync(int userId);
        Task<List<int>> GetWarehouseIdsForUserAsync(int userId);
    }
}
