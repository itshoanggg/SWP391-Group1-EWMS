namespace EWMS.ViewModels
{
    public class SalesOrderListViewModel
    {
        public int SalesOrderID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string WarehouseName { get; set; }

        public List<OrderProductViewModel> Products { get; set; }

        public StockOutReceiptViewModel StockOutReceipt { get; set; }
    }

    public class OrderProductViewModel
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class StockOutReceiptViewModel
    {
        public int StockOutID { get; set; }
        public string IssuedBy { get; set; }
        public DateTime IssuedDate { get; set; }
        public List<StockOutDetailViewModel> Details { get; set; }
    }

    public class StockOutDetailViewModel
    {
        public string ProductName { get; set; }
        public string LocationCode { get; set; }
        public int Quantity { get; set; }
    }

}
