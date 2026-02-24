using EWMS.DTOs;

namespace EWMS.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<KPIMetricsDTO> GetKPIMetricsAsync(int warehouseId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<IEnumerable<StockMovementDTO>> GetStockMovementAsync(int warehouseId, string period = "week");
        Task<IEnumerable<SalesRevenueDTO>> GetSalesRevenueAsync(int warehouseId, string period = "month");
        Task<IEnumerable<CategoryDistributionDTO>> GetCategoryDistributionAsync(int warehouseId);
        Task<IEnumerable<RecentActivityDTO>> GetRecentActivitiesAsync(int warehouseId, int limit = 10);
        Task<IEnumerable<LowStockAlertDTO>> GetLowStockAlertsAsync(int warehouseId, int threshold = 10);
        Task<InventoryStatsDTO> GetInventoryStatsAsync(int warehouseId);
    }
}
