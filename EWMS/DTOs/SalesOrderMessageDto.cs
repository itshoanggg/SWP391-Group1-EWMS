namespace EWMS.DTOs
{
    public class SalesOrderMessageDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
        public List<SalesOrderDetailDto> Details { get; set; } = new();
    }

    public class SalesOrderDetailDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

}
