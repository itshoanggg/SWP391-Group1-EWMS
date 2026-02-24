namespace EWMS.DTOs
{
    public class PurchaseOrderListDTO
    {
        public int PurchaseOrderId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? ExpectedReceivingDate { get; set; }
        public int TotalItems { get; set; }
        public int ReceivedItems { get; set; }
        public int RemainingItems { get; set; }
        public decimal TotalAmount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }

    public class PurchaseOrderInfoDTO
    {
        public int PurchaseOrderId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string? SupplierPhone { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public bool HasStockIn { get; set; }
    }

    public class PurchaseOrderProductDTO
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int OrderedQty { get; set; }
        public int ReceivedQty { get; set; }
        public int RemainingQty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class ProductBySupplierDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
    }
}
