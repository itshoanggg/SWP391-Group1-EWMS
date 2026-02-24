namespace EWMS.ViewModels
{
    public class SalesOrderListViewModel
    {
        public List<SalesOrderViewModel> Orders { get; set; } = new();
        public string WarehouseName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }

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
