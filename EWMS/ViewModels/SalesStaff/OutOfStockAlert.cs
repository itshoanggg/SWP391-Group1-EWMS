namespace EWMS.ViewModels.SalesStaff
{
    public class OutOfStockAlert
    {
        public int SalesOrderId { get; set; }
        public string? CustomerName { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public int RequiredQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public int ShortageQuantity { get; set; }
    }
}
