using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Purchasing Staff")]
    public class PurchaseOrderController : Controller
    {
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly ISupplierService _supplierService;
        private readonly IUserService _userService;

        public PurchaseOrderController(
            IPurchaseOrderService purchaseOrderService,
            ISupplierService supplierService,
            IUserService userService)
        {
            _purchaseOrderService = purchaseOrderService;
            _supplierService = supplierService;
            _userService = userService;
        }

        // GET: PurchaseOrder/Index
        public async Task<IActionResult> Index(string status = "Ordered")
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

            var purchaseOrders = await _purchaseOrderService.GetPurchaseOrdersAsync(warehouseId, status);
            return View(purchaseOrders);
        }

        // GET: PurchaseOrder/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

            var purchaseOrder = await _purchaseOrderService.GetPurchaseOrderByIdAsync(id, warehouseId);

            if (purchaseOrder == null)
            {
                TempData["Error"] = "Không tìm thấy đơn mua hàng.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.TotalQuantity = purchaseOrder.PurchaseOrderDetails.Sum(d => d.Quantity);
            ViewBag.TotalAmount = purchaseOrder.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0);

            return View(purchaseOrder);
        }

        // GET: PurchaseOrder/Create
        public async Task<IActionResult> Create()
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

            ViewBag.Suppliers = new SelectList(
                await _supplierService.GetAllSuppliersAsync(),
                "SupplierId",
                "SupplierName"
            );

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;

            return View();
        }

        // POST: PurchaseOrder/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderCreateViewModel model)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

            if (!ModelState.IsValid || model.Details == null || !model.Details.Any())
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin và ít nhất 1 sản phẩm.";

                ViewBag.Suppliers = new SelectList(
                    await _supplierService.GetAllSuppliersAsync(),
                    "SupplierId",
                    "SupplierName"
                );
                ViewBag.WarehouseId = warehouseId;
                ViewBag.UserId = userId;

                return View(model);
            }

            try
            {
                var purchaseOrder = await _purchaseOrderService.CreatePurchaseOrderAsync(model, warehouseId, userId);

                TempData["Success"] = $"Tạo đơn mua hàng PO-{purchaseOrder.PurchaseOrderId:D4} thành công!";
                return RedirectToAction(nameof(Details), new { id = purchaseOrder.PurchaseOrderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";

                ViewBag.Suppliers = new SelectList(
                    await _supplierService.GetAllSuppliersAsync(),
                    "SupplierId",
                    "SupplierName"
                );
                ViewBag.WarehouseId = warehouseId;
                ViewBag.UserId = userId;

                return View(model);
            }
        }

        // POST: PurchaseOrder/MarkAsDelivered
        [HttpPost]
        public async Task<IActionResult> MarkAsDelivered(int id)
        {
            try
            {
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());
                var result = await _purchaseOrderService.MarkAsDeliveredAsync(id, warehouseId);

                if (!result)
                    return Json(new { success = false, message = "Không thể cập nhật đơn hàng" });

                return Json(new { success = true, message = "Đã cập nhật: Hàng đã về kho" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // API: Get Products by Supplier
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            var products = await _purchaseOrderService.GetProductsBySupplierAsync(supplierId);
            return Json(products);
        }

        // DELETE: PurchaseOrder/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());
            var result = await _purchaseOrderService.DeletePurchaseOrderAsync(id, warehouseId);

            if (!result)
                return Json(new { success = false, message = "Không thể xóa đơn hàng" });

            return Json(new { success = true, message = "Xóa đơn hàng thành công" });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(_userService.GetCurrentUserId());
            var result = await _purchaseOrderService.CancelPurchaseOrderAsync(id, warehouseId);

            if (!result)
                return Json(new { success = false, message = "Không thể hủy đơn hàng" });

            return Json(new { success = true, message = "Đã hủy đơn hàng thành công" });
        }

    }
}
