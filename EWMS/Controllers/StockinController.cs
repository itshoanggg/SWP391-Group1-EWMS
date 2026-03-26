using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EWMS.Services;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Inventory Staff,Warehouse Manager")]
    public class StockInController : Controller
    {
        private readonly IStockInService _stockInService;
        private readonly IUserService _userService;
        private readonly TransferService _transferService;

        public StockInController(
            IStockInService stockInService,
            IUserService userService,
            TransferService transferService)
        {
            _stockInService = stockInService;
            _userService = userService;
            _transferService = transferService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["Error"] = "You have not been assigned to any warehouse.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.PendingTransferStockIns = await _transferService.GetPendingTransferStockInAsync(warehouseId);
            return View();
        }

        public async Task<IActionResult> History()
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["Error"] = "You have not been assigned to any warehouse.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.WarehouseId = warehouseId;
            return View("History");
        }

        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrders(int warehouseId, string status = "", string search = "")
        {
            try
            {
                var userWarehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());

                if (userWarehouseId != warehouseId)
                    return Json(new { error = "You do not have access to this warehouse" });

                var result = await _stockInService.GetPurchaseOrdersForStockInAsync(warehouseId, status, search);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Server error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoryPurchaseOrders(int warehouseId, string search = "")
        {
            try
            {
                var userWarehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());

                if (userWarehouseId != warehouseId)
                    return Json(new { error = "You do not have access to this warehouse" });

                var result = await _stockInService.GetPurchaseOrdersHistoryAsync(warehouseId, search);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Server error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

            var purchaseOrder = await _stockInService.GetPurchaseOrderDetailsAsync(id, warehouseId);

            if (purchaseOrder == null)
            {
                TempData["Error"] = "Purchase order not found.";
                return RedirectToAction(nameof(Index));
            }

            if (purchaseOrder.Status != "PartiallyReceived" && purchaseOrder.Status != "Ordered")
            {
                TempData["Error"] = "Stock-in only allowed for orders ready to receive, partially received, or ordered.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;

            return View(purchaseOrder);
        }

        [HttpGet]
        public async Task<IActionResult> TransferDetails(int transferId)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

            try
            {
                var transfer = await _transferService.GetTransferStockInAsync(transferId, warehouseId);
                if (transfer == null)
                {
                    TempData["Error"] = "Transfer not found or cannot be processed.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.WarehouseId = warehouseId;
                ViewBag.UserId = userId;
                return View("TransferDetails", transfer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize(Roles = "Inventory Staff")]
        [HttpGet]
        public async Task<IActionResult> DetailsReadOnly(int id)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["Error"] = "You have not been assigned to any warehouse.";
                return RedirectToAction("Index", "Home");
            }

            var purchaseOrder = await _stockInService.GetPurchaseOrderDetailsAsync(id, warehouseId);
            if (purchaseOrder == null)
            {
                TempData["Error"] = "Purchase order not found.";
                return RedirectToAction(nameof(History));
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;
            ViewBag.ReadOnly = true;

            return View("~/Views/StockIn/Details.cshtml", purchaseOrder);
        }

        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderInfo(int purchaseOrderId)
        {
            try
            {
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());
                var result = await _stockInService.GetPurchaseOrderInfoAsync(purchaseOrderId, warehouseId);

                if (result == null)
                    return Json(new { error = "Purchase order not found" });

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

        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderAllocations(int purchaseOrderId)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return Json(new { error = "Not authenticated" });
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0) return Json(new { error = "No warehouse" });

            var po = await _stockInService.GetPurchaseOrderDetailsAsync(purchaseOrderId, warehouseId);
            if (po == null) return Json(new { error = "PO not found" });

            var allocations = await _stockInService.GetPurchaseOrderAllocationsAsync(purchaseOrderId);
            return Json(allocations);
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableLocations(int warehouseId, int productId)
        {
            try
            {
                var userWarehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());

                if (userWarehouseId != warehouseId)
                    return Json(new { error = "Access denied" });

                var locations = await _stockInService.GetAvailableLocationsAsync(warehouseId, productId);
                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckLocationCapacity(int locationId)
        {
            try
            {
                var result = await _stockInService.CheckLocationCapacityAsync(locationId);

                if (result == null)
                    return Json(new { error = "Location not found" });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

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
                    message = $"Stock-in successful! Receipt code: SI-{stockInReceipt.StockInId:D4}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmTransferStockIn([FromBody] ConfirmTransferStockInRequest request)
        {
            try
            {
                var userId = _userService.GetCurrentUserId();
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                if (request.WarehouseId != warehouseId)
                    return Json(new { success = false, error = "Không có quyền truy cập" });

                var stockInReceiptId = await _transferService.ProcessTransferStockInAsync(request, userId);

                return Json(new
                {
                    success = true,
                    message = $"Transfer stock-in successful! Receipt code: SI-{stockInReceiptId:D4}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
