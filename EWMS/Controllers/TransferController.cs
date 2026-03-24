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
    /// Authorization: Admin, Inventory Staff, Warehouse Manager
    /// </summary>
    [Authorize(Roles = "Admin,Inventory Staff,Warehouse Manager")]
    public class TransferController : Controller
    {
        private readonly TransferService _transferService;
        private readonly UserService _userService;

        public TransferController(TransferService transferService, UserService userService)
        {
            _transferService = transferService;
            _userService = userService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            
            var isManager = User.IsInRole("Warehouse Manager") || User.IsInRole("Admin");
            
            List<TransferRequest> transfers;
            if (isManager && warehouseId > 0)
            {
                transfers = await _transferService.GetTransfersForWarehouseAsync(warehouseId);
            }
            else
            {
                transfers = await _transferService.GetAllTransfersAsync();
            }
            
            ViewBag.UserWarehouseId = warehouseId;
            ViewBag.IsManager = isManager;
            
            return View(transfers);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            ViewBag.UserWarehouseId = warehouseId;
            
            var warehouses = await _transferService.GetWarehousesAsync();
            if (warehouseId > 0)
            {
                warehouses = warehouses.Where(w => w.WarehouseId != warehouseId).ToList();
            }
            ViewBag.Warehouses = warehouses;
            ViewBag.Products = await _transferService.GetProductsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int FromWarehouseId, int ToWarehouseId, string TransferType, int ProductID, int Quantity, string? Reason)
        {
            ModelState.Remove("FromWarehouse");
            ModelState.Remove("ToWarehouse");
            ModelState.Remove("RequestedByNavigation");
            ModelState.Remove("ApprovedByNavigation");

            if (FromWarehouseId == 0)
            {
                ModelState.AddModelError("", "Please select source warehouse.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            if (ToWarehouseId == 0)
            {
                ModelState.AddModelError("", "Please select destination warehouse.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            if (FromWarehouseId == ToWarehouseId)
            {
                ModelState.AddModelError("", "Source warehouse and destination warehouse must be different.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            if (string.IsNullOrEmpty(TransferType))
            {
                ModelState.AddModelError("", "Please select transfer type.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            if (ProductID == 0)
            {
                ModelState.AddModelError("", "Please select a product.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            if (Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.TryParse(userIdClaim, out var id) ? id : 0;

            if (userId == 0)
            {
                ModelState.AddModelError("", "User not logged in.");
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
                TempData["SuccessMessage"] = "Transfer request created successfully! Waiting for destination warehouse manager approval.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating transfer: {ex.Message}");
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

            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            var isManager = User.IsInRole("Warehouse Manager") || User.IsInRole("Admin");
            
            ViewBag.UserWarehouseId = warehouseId;
            ViewBag.IsManager = isManager;
            ViewBag.CanApprove = isManager && transfer.ToWarehouseId == warehouseId && transfer.Status == "Pending Approval";

            return View(transfer);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Warehouse Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                if (userId == 0)
                {
                    TempData["ErrorMessage"] = "User not authenticated.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _transferService.ApproveTransferAsync(id, userId, warehouseId);
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
        [Authorize(Roles = "Admin,Warehouse Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? rejectionReason)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                if (userId == 0)
                {
                    TempData["ErrorMessage"] = "User not authenticated.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _transferService.RejectTransferAsync(id, userId, warehouseId, rejectionReason);
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
