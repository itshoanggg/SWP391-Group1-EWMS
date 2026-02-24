using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IInventoryRepository : IGenericRepository<Inventory>
    {
        Task<Inventory?> GetByProductAndLocationAsync(int productId, int locationId);
        Task<IEnumerable<Inventory>> GetByWarehouseIdAsync(int warehouseId);
        Task<IEnumerable<Inventory>> GetLowStockAsync(int warehouseId, int threshold);
    }
}
