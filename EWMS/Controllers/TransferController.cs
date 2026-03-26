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
            if (isAdmin)
            {
                transfers = await _transferService.GetAllTransfersAsync();
            }
            else
            {
                transfers = await _transferService.GetTransfersForWarehouseAsync(warehouseId);
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
            ViewBag.Warehouses = warehouses.Where(w => w.WarehouseId != warehouseId).ToList();
            ViewBag.Locations = locations;
            ViewBag.Products = await _transferService.GetProductsAsync();
            
            if (warehouseId > 0)
            {
                ViewBag.FromRacks = await _transferService.GetRacksByWarehouseAsync(warehouseId);
            }
            else
            {
                ViewBag.FromRacks = new List<string>();
            }
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int FromWarehouseId, int FromLocationId, int ProductID, int Quantity, string? Reason, string? FromRack)
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

            if (string.IsNullOrEmpty(FromRack))
            {
                ModelState.AddModelError("", "Please select source rack.");
                await PrepareCreateViewBag(warehouseId);
                return View();
            }

            if (ProductID == 0)
            {
                ModelState.AddModelError("", "Please select a product.");
                await PrepareCreateViewBag(warehouseId);
                return View();
            }

            if (Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0.");
                await PrepareCreateViewBag(warehouseId);
                return View();
            }

            if (userId == 0)
            {
                ModelState.AddModelError("", "User not logged in.");
                await PrepareCreateViewBag(warehouseId);
                return View();
            }

            try
            {
                var model = new TransferRequest
                {
                    FromWarehouseId = FromWarehouseId,
                    ToWarehouseId = null,
                    TransferType = "Transfer",
                    FromRack = FromRack,
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
                await PrepareCreateViewBag(warehouseId);
                return View();
            }
        }

        private async Task PrepareCreateViewBag(int? currentWarehouseId)
        {
            var warehouses = await _transferService.GetWarehousesAsync();
            if (currentWarehouseId > 0)
            {
                warehouses = warehouses.Where(w => w.WarehouseId != currentWarehouseId).ToList();
            }
            ViewBag.Warehouses = warehouses;
            ViewBag.Products = await _transferService.GetProductsAsync();
            
            if (currentWarehouseId > 0)
            {
                ViewBag.FromRacks = await _transferService.GetRacksByWarehouseAsync(currentWarehouseId.Value);
            }
            else
            {
                ViewBag.FromRacks = new List<string>();
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var transfer = await _transferService.GetTransferByIdAsync(id);
            if (transfer == null)
            {
                return NotFound();
            }

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
            ViewBag.CanSetToRack = isManager && transfer.ToWarehouseId > 0 && transfer.Status == "Approved" && string.IsNullOrEmpty(transfer.ToRack);
            
            if (transfer.Status == "Pending" && isManager && transfer.ToWarehouseId.HasValue && transfer.ToWarehouseId.Value > 0)
            {
                ViewBag.ToRacks = await _transferService.GetRacksByWarehouseAsync(transfer.ToWarehouseId.Value);
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
                if (!ToWarehouseId.HasValue || ToWarehouseId.Value == 0)
                {
                    TempData["ErrorMessage"] = "Please select destination warehouse.";
                    return RedirectToAction(nameof(Details), new { id });
                }

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

        [HttpPost]
        [Authorize(Roles = "Admin,Warehouse Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetToRack(int id, string ToRack)
        {
            try
            {
                if (string.IsNullOrEmpty(ToRack))
                {
                    TempData["ErrorMessage"] = "Please select destination rack.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;

                if (userId == 0)
                {
                    TempData["ErrorMessage"] = "User not authenticated.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _transferService.UpdateToRackAsync(id, ToRack, userId);
                TempData["SuccessMessage"] = "Transfer completed successfully! Stock has been moved.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error setting destination rack: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}