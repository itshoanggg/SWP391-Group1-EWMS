using System;

namespace EWMS.ViewModels
{
    public class StockInReceiptItemViewModel
    {
        public int StockInId { get; set; }
        public string ReceiptNumber => $"STOCKIN{StockInId:D4}";
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int ReceivedBy { get; set; }
        public string? ReceivedByName { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string? Reason { get; set; }
        public int? PurchaseOrderId { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
