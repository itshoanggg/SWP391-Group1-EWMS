using EWMS.DTOs;

namespace EWMS.Services.Interfaces
{
    public interface IStockService
    {
        Task<IEnumerable<RackDTO>> GetRacksAsync(int warehouseId);
        Task<IEnumerable<LocationDTO>> GetLocationsByRackAsync(int warehouseId, string rack);
        Task<IEnumerable<ProductInLocationDTO>> GetProductsByLocationAsync(int locationId);
        Task<StockSummaryDTO> GetStockSummaryAsync(int warehouseId);
        Task<bool> PerformInternalTransferAsync(int warehouseId, int fromLocationId, int toLocationId, int productId, int quantity, int userId, string? reason);
    }
}
