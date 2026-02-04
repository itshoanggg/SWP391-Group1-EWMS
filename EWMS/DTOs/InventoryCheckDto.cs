namespace EWMS.DTOs
{
    public class InventoryCheckDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public int CurrentStock { get; set; }
        public int ExpectedIncoming { get; set; }
        public int PendingOutgoing { get; set; }
        public int AvailableStock { get; set; }
        public bool IsAvailable { get; set; }
    }
}
