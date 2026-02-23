using EWMS.Models;

namespace EWMS.Repositories
{
    public interface ISalesOrderRepository
    {
        Task<List<SalesOrder>> GetSalesOrdersByWarehouseAsync(int warehouseId);
        Task<(List<SalesOrder> Orders, int TotalCount)> GetSalesOrdersAsync(int warehouseId, string? customer, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize);
        Task<SalesOrder?> GetSalesOrderByIdAsync(int salesOrderId);
        Task<SalesOrder> CreateSalesOrderAsync(SalesOrder salesOrder);
        Task UpdateSalesOrderAsync(SalesOrder salesOrder);
        Task<bool> UpdateSalesOrderStatusAsync(int salesOrderId, string status);
        Task<(string? Name, string? Address)?> GetLatestCustomerByPhoneAsync(string phone);
        Task<bool> SaveChangesAsync();
    }
}
