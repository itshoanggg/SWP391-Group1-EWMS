using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Services;
using EWMS.Services.Interfaces;
using EWMS.Models;
using EWMS.ViewModels;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Warehouse Manager,Inventory Staff")]
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

        [Authorize(Roles = "Warehouse Manager")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var warehouses = await _transferService.GetWarehousesAsync();
            ViewBag.Warehouses = warehouses;
            ViewBag.Products = await _transferService.GetProductsAsync();
            return View();
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateComplete([FromBody] CreateCompleteTransferRequest model)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var id) ? id : 0;

                if (userId == 0)
                {
                    return Json(new { success = false, error = "User not logged in." });
                }

                if (model.FromWarehouseId == model.ToWarehouseId)
                {
                    return Json(new { success = false, error = "Source and destination warehouses must be different." });
                }

                if (model.Items == null || model.Items.Count == 0)
                {
                    return Json(new { success = false, error = "Please add at least one transfer item." });
                }

                var transferId = await _transferService.CreateCompleteTransferAsync(model, userId);

                return Json(new
                {
                    success = true,
                    message = $"Transfer TR-{transferId:D4} created successfully with Pending status. The transfer will appear in StockOut for the source warehouse to process.",
                    transferId
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += " - " + ex.InnerException.Message;
                }
                return Json(new { success = false, error = $"Error creating transfer: {errorMsg}" });
            }
        }

        public async Task<IActionResult> Details(int id, string? returnUrl = null)
        {
            var transfer = await _transferService.GetTransferByIdAsync(id);
            if (transfer == null)
            {
                return NotFound();
            }

            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            var isManager = User.IsInRole("Warehouse Manager");

            var warehouses = await _transferService.GetWarehousesAsync();
            ViewBag.Warehouses = warehouses;
            ViewBag.UserWarehouseId = warehouseId;
            ViewBag.IsManager = isManager;
            ViewBag.ReturnUrl = returnUrl;

            return View(transfer);
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTransfer(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                await _transferService.ApproveTransferAsync(id, userId, warehouseId);

                TempData["SuccessMessage"] = $"Transfer TR-{id:D4} has been approved successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTransfer(int id, string? rejectionReason)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;
                var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);

                await _transferService.RejectTransferAsync(id, userId, warehouseId, rejectionReason);

                TempData["SuccessMessage"] = $"Transfer TR-{id:D4} has been rejected.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpGet]
        public async Task<IActionResult> GetProductsByWarehouse(int warehouseId)
        {
            try
            {
                var products = await _transferService.GetAvailableProductsByWarehouseAsync(warehouseId);
                return Json(products);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpGet]
        public async Task<IActionResult> GetRacksByWarehouse(int warehouseId)
        {
            try
            {
                var racks = await _transferService.GetRacksByWarehouseAsync(warehouseId);
                return Json(racks);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpGet]
        public async Task<IActionResult> GetLocationsByRack(int warehouseId, string rack)
        {
            try
            {
                var locations = await _transferService.GetLocationsByRackWithCapacityAsync(warehouseId, rack);
                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpGet]
        public async Task<IActionResult> GetProductStockByLocation(int warehouseId, int productId)
        {
            try
            {
                var locations = await _transferService.GetProductStockByLocationAsync(warehouseId, productId);
                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [Authorize(Roles = "Warehouse Manager")]
        [HttpGet]
        public async Task<IActionResult> GetLocationCapacity(int locationId, int productId)
        {
            try
            {
                var capacity = await _transferService.GetLocationCapacityAsync(locationId, productId);
                return Json(capacity);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
