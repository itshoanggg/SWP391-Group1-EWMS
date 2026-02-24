using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IUserWarehouseRepository : IGenericRepository<UserWarehouse>
    {
        Task<int> GetWarehouseIdByUserIdAsync(int userId);
    }
}
