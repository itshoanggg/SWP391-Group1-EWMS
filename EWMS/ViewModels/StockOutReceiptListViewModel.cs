using EWMS.Models;

namespace EWMS.ViewModels
{
    public class StockOutReceiptListViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public List<StockOutReceiptViewModel> Receipts { get; set; } = new List<StockOutReceiptViewModel>();

        // Filters
        public string FilterCustomer { get; set; } = string.Empty;
        public string FilterIssuedBy { get; set; } = string.Empty;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // Pagination
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
