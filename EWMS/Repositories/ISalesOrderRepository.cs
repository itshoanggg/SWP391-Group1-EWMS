using EWMS.Models;

namespace EWMS.Repositories
{
    public interface ISalesOrderRepository
    {
        Task<List<SalesOrder>> GetSalesOrdersByWarehouseAsync(int warehouseId);
        Task<SalesOrder?> GetSalesOrderByIdAsync(int salesOrderId);
        Task<SalesOrder> CreateSalesOrderAsync(SalesOrder salesOrder);
        Task UpdateSalesOrderAsync(SalesOrder salesOrder);
        Task<bool> SaveChangesAsync();
    }
}
