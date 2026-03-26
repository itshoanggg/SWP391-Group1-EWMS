using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class TransferProductStockViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
    }

    public class PendingTransferReceiptViewModel
    {
        public int TransferId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string CounterpartyWarehouseName { get; set; } = string.Empty;
        public string ProductSummary { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public DateTime? RequestedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsProcessed { get; set; }
    }

    public class TransferStockOutPageViewModel
    {
        public int TransferId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string DestinationWarehouseName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public List<TransferStockOutItemViewModel> Details { get; set; } = new();
    }

    public class TransferStockOutItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class CreateTransferStockOutViewModel
    {
        [Required]
        public int TransferId { get; set; }

        [Required]
        public int WarehouseId { get; set; }

        [Required]
        public DateTime IssuedDate { get; set; }

        public List<CreateStockOutDetailViewModel> Details { get; set; } = new();
    }

    public class TransferStockInPageViewModel
    {
        public int TransferId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string SourceWarehouseName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public List<TransferStockInItemViewModel> Details { get; set; } = new();
    }

    public class TransferStockInItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class ConfirmTransferStockInRequest
    {
        [Required]
        public int TransferId { get; set; }

        [Required]
        public int WarehouseId { get; set; }

        public List<ConfirmStockInItem> Items { get; set; } = new();
    }
}
