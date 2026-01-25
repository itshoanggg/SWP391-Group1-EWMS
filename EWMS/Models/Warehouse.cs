namespace EWMS.Models
{
    public class Warehouse
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; }
    }

    public class UserWarehouse
    {
        public int UserId { get; set; }
        public int WarehouseId { get; set; }

        public User User { get; set; }
        public Warehouse Warehouse { get; set; }
    }
}
