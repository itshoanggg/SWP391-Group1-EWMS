namespace EWMS.ViewModels
{
    public class StockOutDetailViewModel
    {
        public int StockOutDetailId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Unit { get; set; }
    }
}
