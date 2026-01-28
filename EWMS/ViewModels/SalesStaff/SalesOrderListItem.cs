namespace EWMS.ViewModels.SalesStaff
{
    public class SalesOrderListItem
    {
        public int SalesOrderId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int TotalQuantity { get; set; }
        public string? Status { get; set; }
        public decimal? TotalAmount { get; set; }
        public bool HasOutOfStock { get; set; }
    }
}
