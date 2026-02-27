namespace EWMS.DTOs
{
    public class PurchaseOrderAllocationDTO
    {
        public int ProductId { get; set; }
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public int Quantity { get; set; }
    }
}
