namespace EWMS.DTOs
{
    public class SalesOrderResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? SalesOrderId { get; set; }
        public int UserId { get; set; }
    }
}
