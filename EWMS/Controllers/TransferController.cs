using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Services;
using EWMS.Services.Interfaces;
using EWMS.Models;

namespace EWMS.Controllers
{
    /// <summary>
    /// Transfer Request Controller
    /// User Story: As an inventory staff, I want to manage transfer requests so that inventory movement is controlled.
    /// Authorization: Admin, Inventory Staff, Warehouse Manager
    /// </summary>
    [Authorize(Roles = "Inventory Staff,Warehouse Manager")]
    public class TransferController : Controller
    {
        private readonly TransferService _transferService;
        private readonly IUserService _userService;

        public TransferController(TransferService transferService, IUserService userService)
        {
            _transferService = transferService;
            _userService = userService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            
            var isAdmin = User.IsInRole("Admin");
            var isManager = User.IsInRole("Warehouse Manager");
            
            List<TransferRequest> transfers;
            if (isAdmin || isManager)
            {
                transfers = await _transferService.GetAllTransfersAsync();
            }
            else
            {
                transfers = await _transferService.GetAllTransfersAsync();
            }
            
            ViewBag.UserWarehouseId = warehouseId;
            ViewBag.IsManager = isManager || isAdmin;
            ViewBag.IsAdmin = isAdmin;
            
            return View(transfers);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            
            var warehouses = await _transferService.GetWarehousesAsync();
            var userWarehouse = warehouses.FirstOrDefault(w => w.WarehouseId == warehouseId);
            var locations = warehouseId > 0 ? await _transferService.GetLocationsByWarehouseAsync(warehouseId) : new List<Location>();
            
            ViewBag.UserWarehouse = userWarehouse;
            ViewBag.UserWarehouseId = warehouseId;
            ViewBag.Warehouses = warehouses;
            ViewBag.Locations = locations;
            ViewBag.Products = await _transferService.GetProductsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int FromWarehouseId, int FromLocationId, int ProductID, int Quantity, string? Reason)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.TryParse(userIdClaim, out var id) ? id : 0;
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            
            FromWarehouseId = warehouseId;
            
            ModelState.Remove("FromWarehouse");
            ModelState.Remove("ToWarehouse");
            ModelState.Remove("RequestedByNavigation");
            ModelState.Remove("ApprovedByNavigation");

            if (FromWarehouseId == 0)
            {
                ModelState.AddModelError("", "You are not assigned to any warehouse.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                return View();
            }

            if (FromLocationId == 0)
            {
                ModelState.AddModelError("", "Please select source location.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                return View();
            }

            if (ProductID == 0)
            {
                ModelState.AddModelError("", "Please select a product.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                return View();
            }

            if (Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                return View();
            }

            if (userId == 0)
            {
                ModelState.AddModelError("", "User not logged in.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                return View();
            }

            try
            {
                var model = new TransferRequest
                {
                    FromWarehouseId = FromWarehouseId,
                    ToWarehouseId = null,
                    TransferType = "Transfer",
                    Reason = Reason
                };

                await _transferService.CreateTransferAsync(model, ProductID, Quantity, userId, FromLocationId);
                TempData["SuccessMessage"] = "Transfer request created successfully! Waiting for warehouse manager to select destination and confirm.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += " - " + ex.InnerException.Message;
                }
                ModelState.AddModelError("", $"Error creating transfer: {errorMsg}");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
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
            var isAdmin = User.IsInRole("Admin");
            var isManager = User.IsInRole("Warehouse Manager");
            
            var warehouses = await _transferService.GetWarehousesAsync();
            ViewBag.Warehouses = warehouses;
            ViewBag.UserWarehouseId = warehouseId;
            ViewBag.IsManager = isManager;
            
            var canSelectDestination = isManager && transfer.Status == "Pending Destination" && transfer.FromWarehouseId == warehouseId;
            var canApproveAtDestination = isManager && transfer.Status == "Pending" && transfer.ToWarehouseId == warehouseId;
            ViewBag.CanApprove = canApproveAtDestination;

            // For Manager approve form: provide destination warehouse id if known
            if (canSelectDestination || canApproveAtDestination)
            {
                var destWarehouseId = canApproveAtDestination ? transfer.ToWarehouseId : null;
                if (destWarehouseId.HasValue)
                    ViewBag.DestWarehouseId = destWarehouseId.Value;
            }

            return View(transfer);
        }

        [HttpPost]
        [Authorize(Roles = "Warehouse Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int? ToWarehouseId, int? ToLocationId)
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

                await _transferService.ApproveTransferAsync(id, userId, warehouseId, false, ToWarehouseId, ToLocationId);
                TempData["SuccessMessage"] = "Transfer confirmed successfully! Goods will be sent to the destination warehouse.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving transfer: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Warehouse Manager")]
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
