namespace EWMS.ViewModels
{
    public class SalesOrderListViewModel
    {
        public List<SalesOrderViewModel> Orders { get; set; } = new();
        public string WarehouseName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
    }
}
