namespace EWMS.ViewModels
{
    public class StockInCreateViewModel
    {
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string PurchaseOrderCode { get; set; } = string.Empty;
        public DateTime? ExpectedReceivingDate { get; set; }
        public List<StockInDetailViewModel> Details { get; set; } = new List<StockInDetailViewModel>();
    }

    public class StockInDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int OrderedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public int CurrentReceiving { get; set; }
        public decimal UnitPrice { get; set; }
        public int LocationId { get; set; }
    }

    public class ConfirmStockInRequest
    {
        public int PurchaseOrderId { get; set; }
        public int WarehouseId { get; set; }
        public List<ConfirmStockInItem> Items { get; set; } = new List<ConfirmStockInItem>();
    }

    public class ConfirmStockInItem
    {
        public int ProductId { get; set; }
        public int LocationId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
