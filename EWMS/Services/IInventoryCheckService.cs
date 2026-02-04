using EWMS.DTOs;

namespace EWMS.Services
{
    public interface IInventoryCheckService
    {
        Task<InventoryCheckResult> CheckInventoryAvailabilityAsync(InventoryCheckRequest request);

    }
}
