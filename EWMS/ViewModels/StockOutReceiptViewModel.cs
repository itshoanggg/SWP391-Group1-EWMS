namespace EWMS.ViewModels
{
    public class StockOutReceiptViewModel
    {
        public int StockOutId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public int? SalesOrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public DateTime? IssuedDate { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public decimal? TotalAmount { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string IssuedByName { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public List<StockOutDetailViewModel> Details { get; set; } = new List<StockOutDetailViewModel>();
    }
}
