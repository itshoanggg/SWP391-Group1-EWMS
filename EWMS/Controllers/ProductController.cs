using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Repositories.Interfaces;
using EWMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Controllers
{
    /// <summary>
    /// Product Information Controller
    /// User Story: As a warehouse manager, I want to view product information so that storage decisions are accurate.
    /// Authorization: Admin, Warehouse Manager
    /// </summary>
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;

        public ProductController(IProductRepository productRepository, IInventoryRepository inventoryRepository)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
        }

        // GET: Product
        public async Task<IActionResult> Index()
        {
            var products = await _productRepository.GetAllProductsAsync();
            var viewModels = products.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryName = p.Category?.CategoryName ?? "N/A",
                Unit = p.Unit ?? "N/A",
                CostPrice = p.CostPrice ?? 0,
                SellingPrice = p.SellingPrice ?? 0
            }).ToList();

            return View(viewModels);
        }

        // GET: Product/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Get inventory information grouped by warehouse and location
            var inventoryItems = await _inventoryRepository.GetInventoryByProductIdAsync(id);

            var viewModel = new ProductDetailsViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                CategoryName = product.Category?.CategoryName ?? "N/A",
                Unit = product.Unit ?? "N/A",
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
    }
}
