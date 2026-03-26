using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    // List view with pagination
    public class ProductListViewModel
    {
        public List<ProductItemViewModel> Products { get; set; } = new List<ProductItemViewModel>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public string? SearchTerm { get; set; }
        public int? FilterCategoryId { get; set; }
        public int? FilterSupplierId { get; set; }
    }

    public class ProductItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string CategoryName { get; set; } = null!;
        public int CategoryId { get; set; }
        public string? SupplierName { get; set; }
        public int SupplierCount { get; set; }
        public string Unit { get; set; } = null!;
        public int TotalStock { get; set; }
    }

    // Details view
    public class ProductDetailsViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string CategoryName { get; set; } = null!;
        public int CategoryId { get; set; }
        public List<string> SupplierNames { get; set; } = new List<string>();
        public string Unit { get; set; } = null!;
        public List<WarehouseInventoryViewModel> InventoryByWarehouse { get; set; } = new List<WarehouseInventoryViewModel>();
        public int TotalInventory { get; set; }
    }

    // Create view
    public class CreateProductViewModel
    {
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(150, ErrorMessage = "Product name cannot exceed 150 characters")]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = null!;

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "New Category Name")]
        public string? NewCategoryName { get; set; }

        [Display(Name = "Suppliers")]
        public List<int> SelectedSupplierIds { get; set; } = new List<int>();

        [Display(Name = "New Supplier Name")]
        public string? NewSupplierName { get; set; }

        // For dropdown lists
        public List<CategoryOptionViewModel> Categories { get; set; } = new List<CategoryOptionViewModel>();
        public List<SupplierOptionViewModel> Suppliers { get; set; } = new List<SupplierOptionViewModel>();
    }

    // Edit view
    public class EditProductViewModel
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(150, ErrorMessage = "Product name cannot exceed 150 characters")]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = null!;

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "New Category Name")]
        public string? NewCategoryName { get; set; }

        [Display(Name = "Suppliers")]
        public List<int> SelectedSupplierIds { get; set; } = new List<int>();

        [Display(Name = "New Supplier Name")]
        public string? NewSupplierName { get; set; }

        public string Unit { get; set; } = "Piece";

        // For dropdown lists
        public List<CategoryOptionViewModel> Categories { get; set; } = new List<CategoryOptionViewModel>();
        public List<SupplierOptionViewModel> Suppliers { get; set; } = new List<SupplierOptionViewModel>();
    }

    // Helper ViewModels
    public class CategoryOptionViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string? SupplierName { get; set; }
        public int? SupplierId { get; set; }
        public int SuggestedMarkupPercent { get; set; } = 25;
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

    public class SupplierOptionViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
    }
}
