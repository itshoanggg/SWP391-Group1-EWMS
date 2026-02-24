namespace EWMS.DTOs
{
    public class KPIMetricsDTO
    {
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public int TodayOrders { get; set; }
        public decimal InventoryValue { get; set; }
    }

    public class StockMovementDTO
    {
        public string Date { get; set; } = string.Empty;
        public int StockIn { get; set; }
        public int StockOut { get; set; }
        public int Stock { get; set; }
    }

    public class SalesRevenueDTO
    {
        public string Date { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CategoryDistributionDTO
    {
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class RecentActivityDTO
    {
        public string Type { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string User { get; set; } = string.Empty;
    }

    public class LowStockAlertDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string LocationCode { get; set; } = string.Empty;
    }

    public class InventoryStatsDTO
    {
        public int TotalProducts { get; set; }
        public int TotalStock { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
    }
}
