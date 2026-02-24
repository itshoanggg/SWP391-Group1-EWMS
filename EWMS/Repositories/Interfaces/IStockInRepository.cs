using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IStockInRepository : IGenericRepository<StockInReceipt>
    {
        Task<IEnumerable<StockInReceipt>> GetByWarehouseIdAsync(int warehouseId);
        Task<Dictionary<int, int>> GetReceivedQuantitiesAsync(int purchaseOrderId);
    }
}
