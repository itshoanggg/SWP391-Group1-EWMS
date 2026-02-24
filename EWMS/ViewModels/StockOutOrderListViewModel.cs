namespace EWMS.ViewModels
{
    public class StockOutOrderListViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public List<SalesOrderForStockOutViewModel> Orders { get; set; } = new();

        // Filters
        public string FilterCustomer { get; set; } = string.Empty;
        public string FilterStatus { get; set; } = string.Empty;

        // Pagination
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
