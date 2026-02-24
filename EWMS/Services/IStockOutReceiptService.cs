using EWMS.DTOs;
using EWMS.ViewModels;

namespace EWMS.Services
{
    public interface IStockOutReceiptService
    {
        Task<StockOutReceiptListViewModel> GetStockOutReceiptsByWarehouseAsync(int warehouseId);
        Task<StockOutReceiptViewModel?> GetStockOutReceiptByIdAsync(int stockOutId);
        Task<List<SalesOrderForStockOutViewModel>> GetPendingSalesOrdersAsync(int warehouseId);
        Task<StockOutOrderListViewModel> GetPendingOrdersForIndexAsync(int warehouseId, string? customer, string? status, int page, int pageSize);
        Task<SalesOrderForStockOutViewModel?> GetSalesOrderForStockOutAsync(int salesOrderId);
        Task<List<LocationInventoryDto>> GetAvailableLocationsForProductAsync(int warehouseId, int productId);
        Task<(bool Success, string Message, int? ReceiptId)> CreateStockOutReceiptAsync(CreateStockOutReceiptViewModel model, int issuedByUserId);
    }
}
