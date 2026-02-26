using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Inventory Staff,Inventory,Warehouse")]
    public class StockInController : Controller
    {
        private readonly IStockInService _stockInService;
        private readonly IUserService _userService;

        public StockInController(
            IStockInService stockInService,
            IUserService userService)
        {
            _stockInService = stockInService;
            _userService = userService;
        }

        // GET: StockIn/Index
        public async Task<IActionResult> Index()
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
            return View();
        }

        // API: Get Purchase Orders
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrders(int warehouseId, string status = "", string search = "")
        {
            try
            {
                var userWarehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());

                if (userWarehouseId != warehouseId)
                    return Json(new { error = "Bạn không có quyền truy cập kho này" });

                var result = await _stockInService.GetPurchaseOrdersForStockInAsync(warehouseId, status, search);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Lỗi server: {ex.Message}" });
            }
        }

        // GET: StockIn/Details/poId
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

            var purchaseOrder = await _stockInService.GetPurchaseOrderDetailsAsync(id, warehouseId);

            if (purchaseOrder == null)
            {
                TempData["Error"] = "Không tìm thấy đơn mua hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (purchaseOrder.Status != "ReadyToReceive" && purchaseOrder.Status != "PartiallyReceived")
            {
                TempData["Error"] = "Chỉ có thể nhập kho cho đơn hàng sẵn sàng nhận hoặc đã nhận một phần.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;

            return View(purchaseOrder);
        }

        // API: Get Purchase Order Info
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderInfo(int purchaseOrderId)
        {
            try
            {
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());
                var result = await _stockInService.GetPurchaseOrderInfoAsync(purchaseOrderId, warehouseId);

                if (result == null)
                    return Json(new { error = "Không tìm thấy đơn hàng" });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderProducts(int purchaseOrderId)
        {
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());
            var products = await _stockInService.GetPurchaseOrderProductsAsync(purchaseOrderId, warehouseId);
            return Json(products);
        }

        // API: Get Available Locations
        [HttpGet]
        public async Task<IActionResult> GetAvailableLocations(int warehouseId, int productId)
        {
            try
            {
                var userWarehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());

                if (userWarehouseId != warehouseId)
                    return Json(new { error = "Không có quyền truy cập" });

                var locations = await _stockInService.GetAvailableLocationsAsync(warehouseId, productId);
                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Check Location Capacity
        [HttpGet]
        public async Task<IActionResult> CheckLocationCapacity(int locationId)
        {
            try
            {
                var result = await _stockInService.CheckLocationCapacityAsync(locationId);

                if (result == null)
                    return Json(new { error = "Không tìm thấy vị trí" });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Confirm Stock In
        [HttpPost]
        public async Task<IActionResult> ConfirmStockIn([FromBody] ConfirmStockInRequest request)
        {
            try
            {
                var userId = _userService.GetCurrentUserId();
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                if (request.WarehouseId != warehouseId)
                    return Json(new { success = false, error = "Không có quyền truy cập" });

                var stockInReceipt = await _stockInService.ConfirmStockInAsync(request, userId);

                return Json(new
                {
                    success = true,
                    message = $"Nhập kho thành công! Mã phiếu: SI-{stockInReceipt.StockInId:D4}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
