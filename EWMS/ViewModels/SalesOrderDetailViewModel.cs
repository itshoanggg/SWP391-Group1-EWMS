namespace EWMS.ViewModels
{
    public class SalesOrderDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
