using Microsoft.AspNetCore.Mvc;
using EWMS.Services.Interfaces;

namespace EWMS.Controllers
{
    public class StockController : Controller
    {
        private readonly IStockService _stockService;
        private readonly IUserService _userService;

        public StockController(
            IStockService stockService,
            IUserService userService)
        {
            _stockService = stockService;
            _userService = userService;
        }

        // GET: Stock/Index
        public async Task<IActionResult> Index(string rack = "A")
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["Error"] = "Bạn chưa được phân công vào kho nào.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.DefaultRack = rack;

            return View();
        }

        // API: Get Racks cho Nav (dùng ở mọi trang)
        [HttpGet]
        public async Task<IActionResult> GetRacksForNav()
        {
            try
            {
                var userId = _userService.GetCurrentUserId();
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                var racks = await _stockService.GetRacksAsync(warehouseId);
                return Json(racks.Select(r => new { rack = r.Rack }));
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
                var userId = _userService.GetCurrentUserId();
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                var locations = await _stockService.GetLocationsByRackAsync(warehouseId, rack);
                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Racks
        [HttpGet]
        public async Task<IActionResult> GetRacks(int warehouseId)
        {
            try
            {
                var racks = await _stockService.GetRacksAsync(warehouseId);
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
                var locations = await _stockService.GetLocationsByRackAsync(warehouseId, rack);
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
                var products = await _stockService.GetProductsByLocationAsync(locationId);
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
                var summary = await _stockService.GetStockSummaryAsync(warehouseId);
                return Json(summary);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
