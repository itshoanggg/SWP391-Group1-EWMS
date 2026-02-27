using System;
using System.Collections.Generic;

namespace EWMS.ViewModels
{
    public class ProductListViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string CategoryName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
    }

    public class ProductDetailsViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string CategoryName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public List<WarehouseInventoryViewModel> InventoryByWarehouse { get; set; } = new List<WarehouseInventoryViewModel>();
        public int TotalInventory { get; set; }
    }

    public class WarehouseInventoryViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public int TotalQuantity { get; set; }
        public List<LocationInventoryViewModel> Locations { get; set; } = new List<LocationInventoryViewModel>();
    }

    public class LocationInventoryViewModel
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = null!;
        public int Quantity { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
