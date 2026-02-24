namespace EWMS.DTOs
{
    public class LocationInventoryDto
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public int ProductId { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
