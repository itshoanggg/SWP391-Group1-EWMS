namespace EWMS.DTOs
{
    public class RackDTO
    {
        public string Rack { get; set; } = string.Empty;
        public int LocationCount { get; set; }
        public int TotalCapacity { get; set; }
        public int CurrentStock { get; set; }
    }

    public class LocationDTO
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public string? Rack { get; set; }
        public int Capacity { get; set; }
        public int CurrentStock { get; set; }
        public int ProductCount { get; set; }
    }

    public class ProductInLocationDTO
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public string? Rack { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class StockSummaryDTO
    {
        public int TotalLocations { get; set; }
        public int TotalCapacity { get; set; }
        public int TotalStock { get; set; }
        public int TotalProducts { get; set; }
        public int AvailableSpace { get; set; }
        public double UtilizationRate { get; set; }
    }

    public class AvailableLocationDTO
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public string Rack { get; set; } = string.Empty;
        public int MaxCapacity { get; set; }
        public int CurrentStock { get; set; }
    }

    public class LocationCapacityDTO
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public int MaxCapacity { get; set; }
        public int CurrentStock { get; set; }
    }
}
