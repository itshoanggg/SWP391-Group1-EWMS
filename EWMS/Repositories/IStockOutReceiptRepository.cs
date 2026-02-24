using EWMS.Models;

namespace EWMS.Repositories
{
    public interface IStockOutReceiptRepository
    {
        Task<List<StockOutReceipt>> GetStockOutReceiptsByWarehouseAsync(int warehouseId);
        Task<StockOutReceipt?> GetStockOutReceiptByIdAsync(int stockOutId);
        Task<StockOutReceipt?> GetStockOutReceiptBySalesOrderIdAsync(int salesOrderId);
        Task<int> GetShippedQuantityAsync(int salesOrderId, int productId);
        Task<StockOutReceipt> CreateStockOutReceiptAsync(StockOutReceipt stockOutReceipt);
        Task<bool> UpdateInventoryAsync(int productId, int locationId, int quantity);
    }
}
