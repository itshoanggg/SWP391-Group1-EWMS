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
                    SupplierName = p.ProductSuppliers?.FirstOrDefault()?.Supplier?.SupplierName,
                    SupplierCount = p.ProductSuppliers?.Count ?? 0,
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

            // Pass categories for filters
            ViewBag.Categories = await _productRepository.GetAllCategoriesWithSupplierAsync();
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
                SupplierNames = product.ProductSuppliers?
                    .Select(ps => ps.Supplier?.SupplierName ?? "Unknown")
                    .ToList() ?? new List<string>(),
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
                            LocationName = i.Location.LocationName ?? "Unknown",
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
                Suppliers = await GetSupplierOptionsAsync()
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

            // Determine unit
            string unit = !string.IsNullOrWhiteSpace(model.NewUnit) 
                ? model.NewUnit.Trim() 
                : (model.Unit ?? "Piece");
            
            // Handle new supplier creation or selection by name (avoid duplicates)
            if (!string.IsNullOrWhiteSpace(model.NewSupplierName))
            {
                var newName = model.NewSupplierName.Trim();
                var existingSupplierByName = await _supplierRepository.FirstOrDefaultAsync(s => s.SupplierName.ToLower() == newName.ToLower());
                model.SelectedSupplierIds ??= new List<int>();
                if (existingSupplierByName != null)
                {
                    model.SelectedSupplierIds.Add(existingSupplierByName.SupplierId);
                }
                else
                {
                    var newSupplier = new Supplier
                    {
                        SupplierName = newName
                    };
                    await _supplierRepository.AddAsync(newSupplier);
                    await _supplierRepository.SaveAsync();
                    model.SelectedSupplierIds.Add(newSupplier.SupplierId);
                }
            }

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
                Unit = unit,
                CostPrice = 0,  // Will be set when first stock-in happens
                SellingPrice = 0  // Will be set when first stock-in happens
            };

            try
            {
                await _productRepository.AddAsync(product);
                await _productRepository.SaveAsync();

                // Add ProductSupplier relationships (deduplicate to avoid conflicts)
                if (model.SelectedSupplierIds != null && model.SelectedSupplierIds.Any())
                {
                    var distinctSupplierIds = model.SelectedSupplierIds.Distinct().ToList();
                    foreach (var supplierId in distinctSupplierIds)
                    {
                        var productSupplier = new ProductSupplier
                        {
                            ProductId = product.ProductId,
                            SupplierId = supplierId
                        };
                        await _productRepository.Context.ProductSuppliers.AddAsync(productSupplier);
                    }
                    await _productRepository.Context.SaveChangesAsync();
                }

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
                SelectedSupplierIds = product.ProductSuppliers?
                    .Select(ps => ps.SupplierId)
                    .ToList() ?? new List<int>(),
                Unit = product.Unit ?? "Piece",
                Categories = await GetCategoryOptionsAsync(),
                Suppliers = await GetSupplierOptionsAsync()
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

            // Determine unit
            string unit = !string.IsNullOrWhiteSpace(model.NewUnit) 
                ? model.NewUnit.Trim() 
                : (model.Unit ?? "Piece");
            
            // Handle new supplier creation or selection by name (avoid duplicates)
            if (!string.IsNullOrWhiteSpace(model.NewSupplierName))
            {
                var newName = model.NewSupplierName.Trim();
                var existingSupplierByName = await _supplierRepository.FirstOrDefaultAsync(s => s.SupplierName.ToLower() == newName.ToLower());
                model.SelectedSupplierIds ??= new List<int>();
                if (existingSupplierByName != null)
                {
                    model.SelectedSupplierIds.Add(existingSupplierByName.SupplierId);
                }
                else
                {
                    var newSupplier = new Supplier
                    {
                        SupplierName = newName
                    };
                    await _supplierRepository.AddAsync(newSupplier);
                    await _supplierRepository.SaveAsync();
                    model.SelectedSupplierIds.Add(newSupplier.SupplierId);
                }
            }

            // Validate
            if (!categoryId.HasValue && string.IsNullOrWhiteSpace(model.NewCategoryName))
            {
                ModelState.AddModelError("", "Please select a category or enter a new category name.");
                model.Categories = await GetCategoryOptionsAsync();
                model.Suppliers = await GetSupplierOptionsAsync();
                return View(model);
            }

            // Update product properties
            product.ProductName = model.ProductName.Trim();
            product.CategoryId = categoryId;
            product.Unit = unit;
            // Note: CostPrice and SellingPrice are NOT updated here - they are managed by StockIn

            _productRepository.Update(product);
            await _productRepository.SaveAsync();

            // Update ProductSupplier relationships using diff to avoid tracking conflicts
            var existingSupplierIds = await _productRepository.Context.ProductSuppliers
                .Where(ps => ps.ProductId == product.ProductId)
                .Select(ps => ps.SupplierId)
                .ToListAsync();

            var selectedSupplierIds = (model.SelectedSupplierIds ?? new List<int>()).Distinct().ToList();

            // Determine which links to remove and which to add
            var supplierIdsToRemove = existingSupplierIds.Except(selectedSupplierIds).ToList();
            var supplierIdsToAdd = selectedSupplierIds.Except(existingSupplierIds).ToList();

            if (supplierIdsToRemove.Any())
            {
                var linksToRemove = await _productRepository.Context.ProductSuppliers
                    .Where(ps => ps.ProductId == product.ProductId && supplierIdsToRemove.Contains(ps.SupplierId))
                    .ToListAsync();
                _productRepository.Context.ProductSuppliers.RemoveRange(linksToRemove);
            }

            foreach (var supplierId in supplierIdsToAdd)
            {
                var productSupplier = new ProductSupplier
                {
                    ProductId = product.ProductId,
                    SupplierId = supplierId
                };
                await _productRepository.Context.ProductSuppliers.AddAsync(productSupplier);
            }

            await _productRepository.Context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' updated successfully!";
            return RedirectToAction(nameof(Index));
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
            var categories = await _productRepository.GetAllCategoriesWithSupplierAsync();
            var category = categories.FirstOrDefault(c => c.CategoryId == categoryId);

            if (category == null)
            {
                return NotFound();
            }

            // Markup suggestions based on category
            var markupPercent = GetSuggestedMarkupForCategory(category.CategoryName);

            return Json(new
            {
                categoryId = category.CategoryId,
                categoryName = category.CategoryName,
                supplierId = (int?)null, // Removed - now managed via ProductSuppliers
                supplierName = "N/A",
                suggestedMarkup = markupPercent
            });
        }

        // Helper methods
        private async Task<List<CategoryOptionViewModel>> GetCategoryOptionsAsync()
        {
            var categories = await _productRepository.GetAllCategoriesWithSupplierAsync();
            return categories.Select(c => new CategoryOptionViewModel
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                SupplierName = null, // Removed - now managed via ProductSuppliers
                SupplierId = null,
                SuggestedMarkupPercent = GetSuggestedMarkupForCategory(c.CategoryName)
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


        private int GetSuggestedMarkupForCategory(string categoryName)
        {
            // Suggested markup percentages based on category
            return categoryName.ToLower() switch
            {
                var s when s.Contains("laptop") || s.Contains("computer") => 25,
                var s when s.Contains("smartphone") || s.Contains("tablet") || s.Contains("phone") => 21,
                var s when s.Contains("audio") || s.Contains("headphone") || s.Contains("speaker") => 44,
                var s when s.Contains("monitor") || s.Contains("display") => 41,
                var s when s.Contains("accessor") || s.Contains("peripheral") || s.Contains("mouse") || s.Contains("keyboard") => 56,
                _ => 25
            };
        }
    }
}
