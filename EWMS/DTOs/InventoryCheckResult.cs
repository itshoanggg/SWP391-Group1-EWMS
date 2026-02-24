namespace EWMS.DTOs
{
    public class InventoryCheckResult
    {
        public bool IsValid { get; set; }
        public List<InventoryCheckDto> CheckDetails { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
