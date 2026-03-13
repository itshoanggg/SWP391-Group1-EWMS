using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels;

public class WarehouseListViewModel
{
    public List<WarehouseItemViewModel> Warehouses { get; set; } = new();
    public string? SearchQuery { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9; // 3x3 grid
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class WarehouseItemViewModel
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string? Address { get; set; }
    public string WarehouseCode { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public int LocationCount { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalUsed { get; set; }
    public int UsagePercentage { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class WarehouseDetailsViewModel
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string? Address { get; set; }
    public string WarehouseCode { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public int LocationCount { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalUsed { get; set; }
    public int UsagePercentage { get; set; }
    public List<LocationItemViewModel> Locations { get; set; } = new();
    
    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }
}

public class CreateWarehouseViewModel
{
    [Required(ErrorMessage = "Tên kho là bắt buộc")]
    [StringLength(100, ErrorMessage = "Tên kho không được vượt quá 100 ký tự")]
    [Display(Name = "Tên kho")]
    public string WarehouseName { get; set; } = null!;

    [Required(ErrorMessage = "Địa chỉ là bắt buộc")]
    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = null!;

    [Required(ErrorMessage = "Ký hiệu kho là bắt buộc")]
    [StringLength(5, MinimumLength = 2, ErrorMessage = "Ký hiệu kho phải từ 2-5 ký tự")]
    [RegularExpression(@"^[A-Z]+$", ErrorMessage = "Ký hiệu kho chỉ chứa chữ in hoa")]
    [Display(Name = "Ký hiệu kho")]
    public string Prefix { get; set; } = null!;

    // Optional: Create locations at the same time
    public List<CreateLocationInWarehouseViewModel> Locations { get; set; } = new();
}

public class CreateLocationInWarehouseViewModel
{
    [Required(ErrorMessage = "Location code is required")]
    [StringLength(50, ErrorMessage = "Location code cannot exceed 50 characters")]
    public string LocationCode { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Location name cannot exceed 100 characters")]
    public string? LocationName { get; set; }

    [Required(ErrorMessage = "Rack is required")]
    [StringLength(20, ErrorMessage = "Rack cannot exceed 20 characters")]
    public string Rack { get; set; } = null!;

    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0")]
    public int Capacity { get; set; }
}

public class EditWarehouseViewModel
{
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "Warehouse name is required")]
    [StringLength(100, ErrorMessage = "Warehouse name cannot exceed 100 characters")]
    [Display(Name = "Warehouse Name")]
    public string WarehouseName { get; set; } = null!;

    [Required(ErrorMessage = "Address is required")]
    [StringLength(255, ErrorMessage = "Address cannot exceed 255 characters")]
    [Display(Name = "Address")]
    public string Address { get; set; } = null!;

    [Required(ErrorMessage = "Warehouse prefix is required")]
    [StringLength(5, MinimumLength = 2, ErrorMessage = "Warehouse prefix must be 2-5 characters")]
    [RegularExpression(@"^[A-Z]+$", ErrorMessage = "Warehouse prefix must contain only uppercase letters")]
    [Display(Name = "Warehouse Prefix")]
    public string Prefix { get; set; } = null!;
}

public class LocationListViewModel
{
    public List<LocationItemViewModel> Locations { get; set; } = new();
    public string? SearchQuery { get; set; }
    public int? FilterWarehouseId { get; set; }
    public List<WarehouseSelectItem> Warehouses { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class LocationItemViewModel
{
    public int LocationId { get; set; }
    public int WarehouseId { get; set; }
    public string LocationCode { get; set; } = null!;
    public string? LocationName { get; set; }
    public string? Rack { get; set; }
    public int Capacity { get; set; }
    public int Used { get; set; }
    public int UsagePercentage { get; set; }
    public string? WarehouseName { get; set; }
}

public class LocationDetailsViewModel
{
    public int LocationId { get; set; }
    public int WarehouseId { get; set; }
    public string LocationCode { get; set; } = null!;
    public string? LocationName { get; set; }
    public string? Rack { get; set; }
    public int Capacity { get; set; }
    public int Used { get; set; }
    public int Available { get; set; }
    public int UsagePercentage { get; set; }
    public string WarehouseName { get; set; } = null!;
    public List<InventoryItemViewModel> InventoryItems { get; set; } = new();
}

public class InventoryItemViewModel
{
    public string ProductName { get; set; } = null!;
    public string? CategoryName { get; set; }
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class CreateLocationViewModel
{
    [Required(ErrorMessage = "Please select a warehouse")]
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "Location code is required")]
    [StringLength(50, ErrorMessage = "Location code cannot exceed 50 characters")]
    [Display(Name = "Location Code")]
    public string LocationCode { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Location name cannot exceed 100 characters")]
    [Display(Name = "Location Name")]
    public string? LocationName { get; set; }

    [Required(ErrorMessage = "Rack is required")]
    [StringLength(20, ErrorMessage = "Rack cannot exceed 20 characters")]
    [Display(Name = "Rack")]
    public string Rack { get; set; } = null!;

    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0")]
    [Display(Name = "Capacity")]
    public int Capacity { get; set; }

    public List<WarehouseSelectItem> Warehouses { get; set; } = new();
}

public class EditLocationViewModel
{
    public int LocationId { get; set; }
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "Location code is required")]
    [StringLength(50, ErrorMessage = "Location code cannot exceed 50 characters")]
    [Display(Name = "Location Code")]
    public string LocationCode { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Location name cannot exceed 100 characters")]
    [Display(Name = "Location Name")]
    public string? LocationName { get; set; }

    [Required(ErrorMessage = "Rack is required")]
    [StringLength(20, ErrorMessage = "Rack cannot exceed 20 characters")]
    [Display(Name = "Rack")]
    public string Rack { get; set; } = null!;

    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0")]
    [Display(Name = "Capacity")]
    public int Capacity { get; set; }

    public int CurrentUsed { get; set; }
}
