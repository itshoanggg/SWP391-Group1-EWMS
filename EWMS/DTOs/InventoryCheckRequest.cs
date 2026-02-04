namespace EWMS.DTOs
{
    public class InventoryCheckRequest
    {
        public int WarehouseId { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public List<ProductQuantityDto> Products { get; set; } = new();
    }
}
