using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Services;
using EWMS.Models;

namespace EWMS.Controllers
{
    /// <summary>
    /// Transfer Request Controller
    /// User Story: As an inventory staff, I want to manage transfer requests so that inventory movement is controlled.
    /// Authorization: Admin, Inventory Staff
    /// </summary>
    [Authorize(Roles = "Admin,Inventory Staff")]
    public class TransferController : Controller
    {
        private readonly TransferService _transferService;

        public TransferController(TransferService transferService)
        {
            _transferService = transferService;
        }

        public async Task<IActionResult> Index()
        {
            var transfers = await _transferService.GetAllTransfersAsync();
            return View(transfers);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
            ViewBag.Products = await _transferService.GetProductsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int FromWarehouseId, int ToWarehouseId, string TransferType, int ProductID, int Quantity, string? Reason)
        {
            // Clear model state for navigation properties
            ModelState.Remove("FromWarehouse");
            ModelState.Remove("ToWarehouse");
            ModelState.Remove("RequestedByNavigation");
            ModelState.Remove("ApprovedByNavigation");

            // Validation: Check warehouses selected
            if (FromWarehouseId == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn kho nguồn.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            if (ToWarehouseId == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn kho đích.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            // Validation: Check same warehouse
            if (FromWarehouseId == ToWarehouseId)
            {
                ModelState.AddModelError("", "Kho nguồn và kho đích phải khác nhau.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            // Validation: Check transfer type
            if (string.IsNullOrEmpty(TransferType))
            {
                ModelState.AddModelError("", "Vui lòng chọn loại chuyển kho.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            // Validation: Check product
            if (ProductID == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn sản phẩm.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            // Validation: Check quantity
            if (Quantity <= 0)
            {
                ModelState.AddModelError("", "Số lượng phải lớn hơn 0.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.TryParse(userIdClaim, out var id) ? id : 0;

            if (userId == 0)
            {
                ModelState.AddModelError("", "Người dùng chưa đăng nhập.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            try
            {
                var model = new TransferRequest
                {
                    FromWarehouseId = FromWarehouseId,
                    ToWarehouseId = ToWarehouseId,
                    TransferType = TransferType,
                    Reason = Reason
                };

                await _transferService.CreateTransferAsync(model, ProductID, Quantity, userId);
                TempData["SuccessMessage"] = "Tạo yêu cầu chuyển kho thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi khi tạo chuyển kho: {ex.Message}");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var transfer = await _transferService.GetTransferByIdAsync(id);
            if (transfer == null)
                return NotFound();

            return View(transfer);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Inventory Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;

                if (userId == 0)
                {
                    TempData["ErrorMessage"] = "User not authenticated.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _transferService.ApproveTransferAsync(id, userId);
                TempData["SuccessMessage"] = "Transfer request approved successfully!";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving transfer: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Inventory Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? rejectionReason)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;

                if (userId == 0)
                {
                    TempData["ErrorMessage"] = "User not authenticated.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _transferService.RejectTransferAsync(id, userId, rejectionReason);
                TempData["SuccessMessage"] = "Transfer request rejected successfully!";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error rejecting transfer: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}
