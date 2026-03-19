namespace EWMS.ViewModels
{
    public class SalesOrderViewModel
    {
        public int SalesOrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public List<SalesOrderDetailViewModel> Details { get; set; } = new();
    }
}
