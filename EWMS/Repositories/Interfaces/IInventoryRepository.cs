using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IInventoryRepository : IGenericRepository<Inventory>
    {
        Task<Inventory?> GetByProductAndLocationAsync(int productId, int locationId);
        Task<IEnumerable<Inventory>> GetByWarehouseIdAsync(int warehouseId);
        Task<IEnumerable<Inventory>> GetLowStockAsync(int warehouseId, int threshold);
        Task<IEnumerable<Inventory>> GetInventoryByProductIdAsync(int productId);
        
        /// <summary>
        /// Lock và lấy available stock cho danh sách products.
        /// Sử dụng UPDLOCK, ROWLOCK để tránh race condition khi tạo Sales Order.
        /// NOTE: Method này phải được gọi trong một transaction!
        /// </summary>
        /// <param name="productIds">Danh sách Product IDs cần lock</param>
        /// <param name="warehouseId">Warehouse ID</param>
        /// <returns>Dictionary mapping ProductId -> Available Stock (Current - Pending)</returns>
        Task<Dictionary<int, int>> GetAndLockAvailableStockAsync(List<int> productIds, int warehouseId);
    }
}
