using EWMS.ViewModels;

namespace EWMS.Services
{
    public interface ISalesOrderService
    {
        Task<SalesOrderListViewModel> GetSalesOrdersByWarehouseAsync(int warehouseId);
        Task<SalesOrderListViewModel> GetSalesOrdersAsync(int warehouseId, string? customer, string? status, int page, int pageSize);
        Task<SalesOrderViewModel?> GetSalesOrderByIdAsync(int salesOrderId);
        Task<(bool Success, string Message, int? OrderId)> CreateSalesOrderAsync(CreateSalesOrderViewModel model, int createdByUserId);
        Task<List<ProductSelectViewModel>> GetProductsForSelectionAsync();
        Task<bool> CancelSalesOrderAsync(int salesOrderId);
        Task<(bool Found, string? Name, string? Address)> GetCustomerByPhoneAsync(string phone);
    }

    
}