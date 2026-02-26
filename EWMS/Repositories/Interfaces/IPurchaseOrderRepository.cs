using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IPurchaseOrderRepository : IGenericRepository<PurchaseOrder>
    {
        Task<IEnumerable<PurchaseOrder>> GetByWarehouseIdAsync(int warehouseId, string? status = null);
        Task<PurchaseOrder?> GetByIdWithDetailsAsync(int id, int warehouseId);
        Task UpdateToReadyToReceiveAsync(int warehouseId);
    }
}
