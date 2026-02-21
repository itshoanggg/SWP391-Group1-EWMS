using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;


namespace EWMS.Controllers
{
    public class StockController : Controller
    {
        private readonly EWMSContext _context;

        public StockController(EWMSContext context)
        {
            _context = context;
        }

        // GET: Stock/Index
        public async Task<IActionResult> Index(string rack = "A")
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (warehouseId == 0)
            {
                TempData["Error"] = "Bạn chưa được phân công vào kho nào.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.DefaultRack = rack;

            return View();
        }

        // =========================================================
        // API dùng cho Nav Sidebar (_Layout.cshtml)
        // Không cần warehouseId parameter - tự lấy từ user hiện tại
        // =========================================================

        // API: Get Racks cho Nav (dùng ở mọi trang)
        [HttpGet]
        public async Task<IActionResult> GetRacksForNav()
        {
            try
            {
                var userId = GetCurrentUserId();
                var warehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == userId)
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                var racks = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId && l.Rack != null)
                    .GroupBy(l => l.Rack)
                    .Select(g => new { rack = g.Key })
                    .OrderBy(r => r.rack)
                    .ToListAsync();

                return Json(racks);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Locations cho Nav (dùng ở mọi trang)
        [HttpGet]
        public async Task<IActionResult> GetLocationsForNav(string rack)
        {
            try
            {
                var userId = GetCurrentUserId();
                var warehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == userId)
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                var locations = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId && l.Rack == rack)
                    .Select(l => new
                    {
                        locationId = l.LocationId,
                        locationCode = l.LocationCode,
                        capacity = l.Capacity,
                        currentStock = l.Inventories.Sum(i => i.Quantity ?? 0)
                    })
                    .OrderBy(l => l.locationCode)
                    .ToListAsync();

                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // =========================================================
        // API dùng cho trang Stock (Stock.js)
        // =========================================================

        // API: Get Racks
        [HttpGet]
        public async Task<IActionResult> GetRacks(int warehouseId)
        {
            try
            {
                var racks = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId && l.Rack != null)
                    .GroupBy(l => l.Rack)
                    .Select(g => new
                    {
                        rack = g.Key,
                        locationCount = g.Count(),
                        totalCapacity = g.Sum(l => l.Capacity),
                        currentStock = g
                            .SelectMany(l => l.Inventories)
                            .Sum(i => (int?)i.Quantity) ?? 0
                    })
                    .OrderBy(r => r.rack)
                    .ToListAsync();

                return Json(racks);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Locations by Rack
        [HttpGet]
        public async Task<IActionResult> GetLocationsByRack(int warehouseId, string rack)
        {
            try
            {
                var locations = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId && l.Rack == rack)
                    .Select(l => new
                    {
                        locationId = l.LocationId,
                        locationCode = l.LocationCode,
                        locationName = l.LocationName,
                        rack = l.Rack,
                        capacity = l.Capacity,
                        currentStock = l.Inventories.Sum(i => i.Quantity ?? 0),
                        productCount = l.Inventories.Count(i => i.Quantity > 0)
                    })
                    .OrderBy(l => l.locationCode)
                    .ToListAsync();

                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Products by Location
        [HttpGet]
        public async Task<IActionResult> GetProductsByLocation(int locationId)
        {
            try
            {
                var products = await _context.Inventories
                    .Include(i => i.Product)
                        .ThenInclude(p => p.Category)
                    .Include(i => i.Location)
                    .Where(i => i.LocationId == locationId && i.Quantity > 0)
                    .Select(i => new
                    {
                        productId = i.ProductId,
                        sku = $"SKU-{i.ProductId:D5}",
                        productName = i.Product.ProductName,
                        categoryName = i.Product.Category.CategoryName,
                        quantity = i.Quantity,
                        locationCode = i.Location.LocationCode,
                        locationName = i.Location.LocationName,
                        rack = i.Location.Rack,
                        lastUpdated = i.LastUpdated
                    })
                    .OrderBy(p => p.productName)
                    .ToListAsync();

                return Json(products);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Stock Summary
        [HttpGet]
        public async Task<IActionResult> GetStockSummary(int warehouseId)
        {
            try
            {
                var totalLocations = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId)
                    .CountAsync();

                var totalCapacity = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId)
                    .SumAsync(l => l.Capacity);

                var totalStock = await _context.Inventories
                    .Where(i => i.Location.WarehouseId == warehouseId)
                    .SumAsync(i => i.Quantity ?? 0);

                var totalProducts = await _context.Inventories
                    .Where(i => i.Location.WarehouseId == warehouseId && i.Quantity > 0)
                    .Select(i => i.ProductId)
                    .Distinct()
                    .CountAsync();

                var utilizationRate = totalCapacity > 0
                    ? Math.Round((double)totalStock / totalCapacity * 100, 2)
                    : 0;

                return Json(new
                {
                    totalLocations,
                    totalCapacity,
                    totalStock,
                    totalProducts,
                    availableSpace = totalCapacity - totalStock,
                    utilizationRate
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private int GetCurrentUserId()
        {
            return 4; // TODO: Replace with actual auth
        }
    }
}