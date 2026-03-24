using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Repositories.Interfaces;
using EWMS.ViewModels;
using EWMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Controllers
{
  
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ISupplierRepository _supplierRepository;

        public ProductController(
            IProductRepository productRepository, 
            IInventoryRepository inventoryRepository,
            ISupplierRepository supplierRepository)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
            _supplierRepository = supplierRepository;
        }

        // GET: Product
        public async Task<IActionResult> Index(int page = 1, string? search = null, int? categoryId = null, int? supplierId = null)
        {
            const int pageSize = 10;

            var (products, totalCount) = await _productRepository.GetProductsPagedAsync(
                page, pageSize, search, categoryId, supplierId);

            var viewModel = new ProductListViewModel
            {
                Products = products.Select(p => new ProductItemViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.Category?.CategoryName ?? "N/A",
                    CategoryId = p.CategoryId ?? 0,
                    SupplierName = p.Supplier?.SupplierName,
                    Unit = p.Unit ?? "Unit",
                    TotalStock = p.Inventories?.Sum(i => i.Quantity ?? 0) ?? 0
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                SearchTerm = search,
                FilterCategoryId = categoryId,
                FilterSupplierId = supplierId
            };

            // Pass categories and suppliers for filters
            ViewBag.Categories = await _productRepository.Context.ProductCategories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            ViewBag.Suppliers = await _supplierRepository.GetAllOrderedByNameAsync();

            return View(viewModel);
        }

        // GET: Product/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            // Get inventory information grouped by warehouse and location
            var inventoryItems = await _inventoryRepository.GetInventoryByProductIdAsync(id);

            var viewModel = new ProductDetailsViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                CategoryName = product.Category?.CategoryName ?? "N/A",
                CategoryId = product.CategoryId ?? 0,
                SupplierName = product.Supplier?.SupplierName,
                Unit = product.Unit ?? "Unit",
                InventoryByWarehouse = inventoryItems
                    .GroupBy(i => new { i.Location.WarehouseId, i.Location.Warehouse.WarehouseName })
                    .Select(g => new WarehouseInventoryViewModel
                    {
                        WarehouseId = g.Key.WarehouseId,
                        WarehouseName = g.Key.WarehouseName,
                        TotalQuantity = g.Sum(i => i.Quantity ?? 0),
                        Locations = g.Select(i => new LocationInventoryViewModel
                        {
                            LocationId = i.LocationId,
                            LocationName = i.Location.LocationName,
                            Quantity = i.Quantity ?? 0,
                            LastUpdated = i.LastUpdated
                        }).ToList()
                    }).ToList(),
                TotalInventory = inventoryItems.Sum(i => i.Quantity ?? 0)
            };

            return View(viewModel);
        }

        // GET: Product/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateProductViewModel
            {
                Categories = await GetCategoryOptionsAsync(),
                Suppliers = await GetSupplierOptionsAsync(),
                Units = await GetDistinctUnitsAsync()
            };

            return View(viewModel);
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateProductViewModel model)
        {
            // Handle new category creation
            int? categoryId = model.CategoryId;
            if (!string.IsNullOrWhiteSpace(model.NewCategoryName))
            {
                var newCategory = new ProductCategory
                {
                    CategoryName = model.NewCategoryName.Trim()
                };
                await _productRepository.Context.ProductCategories.AddAsync(newCategory);
                await _productRepository.Context.SaveChangesAsync();
                categoryId = newCategory.CategoryId;
            }

            // Handle new supplier creation
            int? supplierId = model.SupplierId;
            if (!string.IsNullOrWhiteSpace(model.NewSupplierName))
            {
                var newSupplier = new Supplier
                {
                    SupplierName = model.NewSupplierName.Trim()
                };
                await _supplierRepository.AddAsync(newSupplier);
                await _supplierRepository.SaveAsync();
                supplierId = newSupplier.SupplierId;
            }

            // Determine unit
            string unit = !string.IsNullOrWhiteSpace(model.NewUnit) 
                ? model.NewUnit.Trim() 
                : (model.Unit ?? "Piece");

            // Validate category
            if (!categoryId.HasValue && string.IsNullOrWhiteSpace(model.NewCategoryName))
            {
                TempData["ErrorMessage"] = "Please select a category or enter a new category name.";
                return RedirectToAction(nameof(Index));
            }

            // Check for duplicate product name
            var productName = model.ProductName.Trim();
            var existingProduct = await _productRepository.Context.Products
                .Where(p => p.ProductName.ToLower() == productName.ToLower())
                .FirstOrDefaultAsync();
            
            if (existingProduct != null)
            {
                TempData["ErrorMessage"] = $"Product '{productName}' already exists!";
                return RedirectToAction(nameof(Index));
            }

            var product = new Product
            {
                ProductName = productName,
                CategoryId = categoryId,
                SupplierId = supplierId,
                Unit = unit,
                CostPrice = 0,  // Will be set when first stock-in happens
                SellingPrice = 0  // Will be set when first stock-in happens
            };

            try
            {
                await _productRepository.AddAsync(product);
                await _productRepository.SaveAsync();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' created successfully! Prices will be set automatically when you stock in.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error creating product: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Product/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new EditProductViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                CategoryId = product.CategoryId,
                SupplierId = product.SupplierId,
                Unit = product.Unit ?? "Piece",
                Categories = await GetCategoryOptionsAsync(),
                Suppliers = await GetSupplierOptionsAsync(),
                Units = await GetDistinctUnitsAsync()
            };

            return View(viewModel);
        }

        // POST: Product/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, EditProductViewModel model)
        {
            if (id != model.ProductId)
            {
                return BadRequest();
            }

            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            // Handle new category creation
            int? categoryId = model.CategoryId;
            if (!string.IsNullOrWhiteSpace(model.NewCategoryName))
            {
                var newCategory = new ProductCategory
                {
                    CategoryName = model.NewCategoryName.Trim()
                };
                await _productRepository.Context.ProductCategories.AddAsync(newCategory);
                await _productRepository.Context.SaveChangesAsync();
                categoryId = newCategory.CategoryId;
            }

            // Handle new supplier creation
            int? supplierId = model.SupplierId;
            if (!string.IsNullOrWhiteSpace(model.NewSupplierName))
            {
                var newSupplier = new Supplier
                {
                    SupplierName = model.NewSupplierName.Trim()
                };
                await _supplierRepository.AddAsync(newSupplier);
                await _supplierRepository.SaveAsync();
                supplierId = newSupplier.SupplierId;
            }

            // Determine unit
            string unit = !string.IsNullOrWhiteSpace(model.NewUnit) 
                ? model.NewUnit.Trim() 
                : (model.Unit ?? "Piece");

            // Validate
            if (!categoryId.HasValue && string.IsNullOrWhiteSpace(model.NewCategoryName))
            {
                ModelState.AddModelError("", "Please select a category or enter a new category name.");
                model.Categories = await GetCategoryOptionsAsync();
                model.Suppliers = await GetSupplierOptionsAsync();
                model.Units = await GetDistinctUnitsAsync();
                return View(model);
            }

            // Update product properties
            product.ProductName = model.ProductName.Trim();
            product.CategoryId = categoryId;
            product.SupplierId = supplierId;
            product.Unit = unit;
            // Note: CostPrice and SellingPrice are NOT updated here - they are managed by StockIn

            _productRepository.Update(product);
            await _productRepository.SaveAsync();

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' updated successfully!";
            return RedirectToAction(nameof(Details), new { id = product.ProductId });
        }

        // POST: Product/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check if product has inventory
            var hasInventory = await _inventoryRepository.GetInventoryByProductIdAsync(id);
            if (hasInventory.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it has inventory records in {hasInventory.Count()} location(s).";
                return RedirectToAction(nameof(Index));
            }

            // Check if product has purchase order details
            var hasPurchaseOrders = await _productRepository.Context.PurchaseOrderDetails
                .Where(pod => pod.ProductId == id)
                .AnyAsync();
            if (hasPurchaseOrders)
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it is referenced in purchase orders.";
                return RedirectToAction(nameof(Index));
            }

            // Check if product has sales order details
            var hasSalesOrders = await _productRepository.Context.SalesOrderDetails
                .Where(sod => sod.ProductId == id)
                .AnyAsync();
            if (hasSalesOrders)
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it is referenced in sales orders.";
                return RedirectToAction(nameof(Index));
            }

            // Check if product has stock in details
            var hasStockIn = await _productRepository.Context.StockInDetails
                .Where(sid => sid.ProductId == id)
                .AnyAsync();
            if (hasStockIn)
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it has stock in history.";
                return RedirectToAction(nameof(Index));
            }

            // Check if product has stock out details
            var hasStockOut = await _productRepository.Context.StockOutDetails
                .Where(sod => sod.ProductId == id)
                .AnyAsync();
            if (hasStockOut)
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it has stock out history.";
                return RedirectToAction(nameof(Index));
            }

            // Check if product has transfer details
            var hasTransfers = await _productRepository.Context.TransferDetails
                .Where(td => td.ProductId == id)
                .AnyAsync();
            if (hasTransfers)
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it is referenced in transfer requests.";
                return RedirectToAction(nameof(Index));
            }

            // If no related records found, proceed with delete
            try
            {
                _productRepository.Delete(product);
                await _productRepository.SaveAsync();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting product: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // API endpoint to get category details (for AJAX)
        [HttpGet]
        public async Task<IActionResult> GetCategoryDetails(int categoryId)
        {
            var category = await _productRepository.Context.ProductCategories
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            if (category == null)
            {
                return NotFound();
            }

            return Json(new
            {
                categoryId = category.CategoryId,
                categoryName = category.CategoryName
            });
        }

        // Helper methods
        private async Task<List<CategoryOptionViewModel>> GetCategoryOptionsAsync()
        {
            var categories = await _productRepository.Context.ProductCategories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            return categories.Select(c => new CategoryOptionViewModel
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName
            }).ToList();
        }

        private async Task<List<SupplierOptionViewModel>> GetSupplierOptionsAsync()
        {
            var suppliers = await _supplierRepository.GetAllOrderedByNameAsync();
            return suppliers.Select(s => new SupplierOptionViewModel
            {
                SupplierId = s.SupplierId,
                SupplierName = s.SupplierName
            }).ToList();
        }

        private async Task<List<string>> GetDistinctUnitsAsync()
        {
            var units = await _productRepository.Context.Products
                .Where(p => p.Unit != null)
                .Select(p => p.Unit!)
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync();
            
            // Add default units if not present
            var defaultUnits = new[] { "Piece", "Box", "Unit", "Set", "Pack" };
            foreach (var unit in defaultUnits)
            {
                if (!units.Contains(unit))
                {
                    units.Add(unit);
                }
            }
            
            return units.OrderBy(u => u).ToList();
        }
    }
}
