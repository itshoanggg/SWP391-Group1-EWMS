using EWMS.Models;

namespace EWMS.Repositories
{
    public interface IInventoryRepository
    {
        Task<int> GetCurrentStockAsync(int productId, int warehouseId);
        Task<int> GetPendingOutgoingAsync(int productId, int warehouseId);
        Task<List<Inventory>> GetInventoryByWarehouseAsync(int warehouseId);
        Task<Inventory?> GetInventoryByProductAndLocationAsync(int productId, int locationId);
    }
}
