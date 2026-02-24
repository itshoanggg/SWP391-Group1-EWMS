using EWMS.DTOs;
using EWMS.Models;
using EWMS.ViewModels;

namespace EWMS.Services.Interfaces
{
    public interface IStockInService
    {
        Task<IEnumerable<PurchaseOrderListDTO>> GetPurchaseOrdersForStockInAsync(int warehouseId, string? status, string? search);
        Task<PurchaseOrder?> GetPurchaseOrderDetailsAsync(int purchaseOrderId, int warehouseId);
        Task<PurchaseOrderInfoDTO?> GetPurchaseOrderInfoAsync(int purchaseOrderId, int warehouseId);
        Task<IEnumerable<PurchaseOrderProductDTO>> GetPurchaseOrderProductsAsync(int purchaseOrderId, int warehouseId);
        Task<StockInReceipt> CreateStockInAsync(StockInCreateViewModel model, int warehouseId, int userId);
        Task<StockInReceipt> ConfirmStockInAsync(ConfirmStockInRequest request, int userId);
        Task<IEnumerable<AvailableLocationDTO>> GetAvailableLocationsAsync(int warehouseId, int productId);
        Task<LocationCapacityDTO?> CheckLocationCapacityAsync(int locationId);
    }
}
