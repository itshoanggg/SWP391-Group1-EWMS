using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IUserWarehouseRepository : IGenericRepository<UserWarehouse>
    {
        Task<int> GetWarehouseIdByUserIdAsync(int userId);
        Task<List<Warehouse>> GetWarehousesForUserAsync(int userId);
        Task<List<int>> GetWarehouseIdsForUserAsync(int userId);
    }
}
