using EWMS.ViewModels;

namespace EWMS.Services
{
    public interface ISalesOrderService
    {
        Task<SalesOrderListViewModel> GetSalesOrdersByWarehouseAsync(int warehouseId);
        Task<SalesOrderViewModel?> GetSalesOrderByIdAsync(int salesOrderId);
        Task<(bool Success, string Message, int? OrderId)> CreateSalesOrderAsync(CreateSalesOrderViewModel model, int createdByUserId);
        Task<List<ProductSelectViewModel>> GetProductsForSelectionAsync();
    }

    public class ProductSelectViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}