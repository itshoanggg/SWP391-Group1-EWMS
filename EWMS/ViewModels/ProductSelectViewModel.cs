namespace EWMS.ViewModels
{
    public class ProductSelectViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
