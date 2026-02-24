using Microsoft.AspNetCore.Mvc;
using EWMS.Services.Interfaces;

namespace EWMS.Controllers
{
    public class InventoryDashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;

        public InventoryDashboardController(
            IDashboardService dashboardService,
            IUserService userService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
        }

        // GET: Dashboard/Index
        public async Task<IActionResult> InventoryDashboard()
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
            ViewBag.UserId = userId;

            return View();
        }

        // API: Get KPI Metrics
        [HttpGet]
        public async Task<IActionResult> GetKPIMetrics(int warehouseId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var metrics = await _dashboardService.GetKPIMetricsAsync(warehouseId, fromDate, toDate);
                return Json(metrics);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Stock Movement Chart Data
        [HttpGet]
        public async Task<IActionResult> GetStockMovement(int warehouseId, string period = "week")
        {
            try
            {
                var result = await _dashboardService.GetStockMovementAsync(warehouseId, period);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Sales Revenue Chart Data
        [HttpGet]
        public async Task<IActionResult> GetSalesRevenue(int warehouseId, string period = "month")
        {
            try
            {
                var result = await _dashboardService.GetSalesRevenueAsync(warehouseId, period);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Product Category Distribution
        [HttpGet]
        public async Task<IActionResult> GetCategoryDistribution(int warehouseId)
        {
            try
            {
                var categoryData = await _dashboardService.GetCategoryDistributionAsync(warehouseId);
                return Json(categoryData);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Recent Activities
        [HttpGet]
        public async Task<IActionResult> GetRecentActivities(int warehouseId, int limit = 10)
        {
            try
            {
                var activities = await _dashboardService.GetRecentActivitiesAsync(warehouseId, limit);
                return Json(activities);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Low Stock Alerts
        [HttpGet]
        public async Task<IActionResult> GetLowStockAlerts(int warehouseId, int threshold = 10)
        {
            try
            {
                var lowStockProducts = await _dashboardService.GetLowStockAlertsAsync(warehouseId, threshold);
                return Json(lowStockProducts);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Inventory Stats for KPI Cards
        [HttpGet]
        public async Task<IActionResult> GetInventoryStats(int warehouseId)
        {
            try
            {
                var stats = await _dashboardService.GetInventoryStatsAsync(warehouseId);
                return Json(stats);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
