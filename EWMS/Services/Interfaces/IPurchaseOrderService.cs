using EWMS.DTOs;
using EWMS.Models;
using EWMS.ViewModels;

namespace EWMS.Services.Interfaces
{
    public interface IPurchaseOrderService
    {
        Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersAsync(int warehouseId, string? status = null);
        Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(int id, int warehouseId);
        Task<PurchaseOrder> CreatePurchaseOrderAsync(PurchaseOrderCreateViewModel model, int warehouseId, int userId);
        Task<bool> CancelPurchaseOrderAsync(int id, int warehouseId, int userId);
        Task<IEnumerable<ProductBySupplierDTO>> GetProductsBySupplierAsync(int supplierId);
    }
}
