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
                    SupplierName = p.Category?.Supplier?.SupplierName,
                    Unit = p.Unit ?? "Unit",
                    CostPrice = p.CostPrice ?? 0,
                    SellingPrice = p.SellingPrice ?? 0
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                SearchTerm = search,
                FilterCategoryId = categoryId,
                FilterSupplierId = supplierId
            };

            // Pass categories and suppliers for filters
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
                SupplierName = product.Category?.Supplier?.SupplierName,
                Unit = product.Unit ?? "Unit",
                CostPrice = product.CostPrice ?? 0,
                SellingPrice = product.SellingPrice ?? 0,
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
                Categories = await GetCategoryOptionsAsync()
            };

            return View(viewModel);
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate selling price >= cost price
                if (model.SellingPrice < model.CostPrice)
                {
                    ModelState.AddModelError("SellingPrice", "Selling price must be greater than or equal to cost price.");
                    model.Categories = await GetCategoryOptionsAsync();
                    return View(model);
                }

                var product = new Product
                {
                    ProductName = model.ProductName.Trim(),
                    CategoryId = model.CategoryId,
                    Unit = model.Unit.Trim(),
                    CostPrice = model.CostPrice,
                    SellingPrice = model.SellingPrice
                };

                await _productRepository.AddAsync(product);
                await _productRepository.SaveAsync();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' created successfully!";
                return RedirectToAction(nameof(Details), new { id = product.ProductId });
            }

            model.Categories = await GetCategoryOptionsAsync();
            return View(model);
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
                CategoryId = product.CategoryId ?? 0,
                Unit = product.Unit ?? "Unit",
                CostPrice = product.CostPrice ?? 0,
                SellingPrice = product.SellingPrice ?? 0,
                Categories = await GetCategoryOptionsAsync()
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

            if (ModelState.IsValid)
            {
                // Validate selling price >= cost price
                if (model.SellingPrice < model.CostPrice)
                {
                    ModelState.AddModelError("SellingPrice", "Selling price must be greater than or equal to cost price.");
                    model.Categories = await GetCategoryOptionsAsync();
                    return View(model);
                }

                var product = await _productRepository.GetProductByIdAsync(id);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Update properties
                product.ProductName = model.ProductName.Trim();
                product.CategoryId = model.CategoryId;
                product.Unit = model.Unit.Trim();
                product.CostPrice = model.CostPrice;
                product.SellingPrice = model.SellingPrice;

                _productRepository.Update(product);
                await _productRepository.SaveAsync();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' updated successfully!";
                return RedirectToAction(nameof(Details), new { id = product.ProductId });
            }

            model.Categories = await GetCategoryOptionsAsync();
            return View(model);
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

            // Check if product has related data
            var hasInventory = await _inventoryRepository.GetInventoryByProductIdAsync(id);
            if (hasInventory.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete '{product.ProductName}' because it has inventory records. Please remove all inventory first.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                _productRepository.Delete(product);
                await _productRepository.SaveAsync();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting product: {ex.Message}. This product may be referenced by other records.";
                return RedirectToAction(nameof(Details), new { id });
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
                supplierId = category.SupplierId,
                supplierName = category.Supplier?.SupplierName ?? "N/A",
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
                SupplierName = c.Supplier?.SupplierName,
                SupplierId = c.SupplierId,
                SuggestedMarkupPercent = GetSuggestedMarkupForCategory(c.CategoryName)
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
