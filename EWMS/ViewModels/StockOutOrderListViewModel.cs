namespace EWMS.ViewModels
{
    public class StockOutOrderListViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public List<SalesOrderForStockOutViewModel> Orders { get; set; } = new();
        public List<PendingTransferForStockOutViewModel> TransferOrders { get; set; } = new();

        // Filters
        public string FilterCustomer { get; set; } = string.Empty;
        public string FilterStatus { get; set; } = string.Empty;

        // Pagination for Sales Orders
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        
        // Pagination for Transfer Orders
        public int TransferPage { get; set; }
        public int TransferPageSize { get; set; }
        public int TransferTotalCount { get; set; }
        public int TransferTotalPages { get; set; }
    }
    
    public class PendingTransferForStockOutViewModel
    {
        public int TransferId { get; set; }
        public string TransferNumber { get; set; } = string.Empty;
        public string DestinationWarehouse { get; set; } = string.Empty;
        public string ProductSummary { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public DateTime? RequestedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool HasStockOutReceipt { get; set; }
        public int? StockOutReceiptId { get; set; }
    }
}
