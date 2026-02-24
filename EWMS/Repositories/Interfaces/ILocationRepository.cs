using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface ILocationRepository : IGenericRepository<Location>
    {
        Task<IEnumerable<Location>> GetByWarehouseIdAsync(int warehouseId);
        Task<IEnumerable<Location>> GetByRackAsync(int warehouseId, string rack);
        Task<IEnumerable<string>> GetRacksAsync(int warehouseId);
    }
}
