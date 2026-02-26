using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IWarehouseRepository : IGenericRepository<Warehouse>
    {
        Task<string?> GetWarehouseNameByIdAsync(int warehouseId);
    }
}
