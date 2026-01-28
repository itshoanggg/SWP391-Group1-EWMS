namespace EWMS.ViewModels.SalesStaff
{
    public class DashboardViewModel
    {
        public DashboardMetrics Metrics { get; set; } = new DashboardMetrics();
        public List<SalesOrderListItem> RecentOrders { get; set; } = new List<SalesOrderListItem>();
        public List<OutOfStockAlert> OutOfStockAlerts { get; set; } = new List<OutOfStockAlert>();
        public int CurrentUserId { get; set; }
        public string CurrentUserName { get; set; } = string.Empty;
        public int CurrentWarehouseId { get; set; }
        public string CurrentWarehouseName { get; set; } = string.Empty;
    }
}
