using EWMS.DTOs;
using EWMS.Services;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Sales Staff")]
    public class SalesOrderController : Controller
    {
        private readonly ISalesOrderService _salesOrderService;
        private readonly IInventoryCheckService _inventoryCheckService;
        private readonly IUserService _userService;

        public SalesOrderController(
            ISalesOrderService salesOrderService,
            IInventoryCheckService inventoryCheckService,
            IUserService userService)
        {
            _salesOrderService = salesOrderService;
            _inventoryCheckService = inventoryCheckService;
            _userService = userService;
        }

        public async Task<IActionResult> Index(string? customer, string? status, int page = 1)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            int warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["ErrorMessage"] = "You haven't been assigned to any warehouse yet.";
                return RedirectToAction("Index", "Home");
            }
            const int pageSize = 5;

            var viewModel = await _salesOrderService
                .GetSalesOrdersAsync(warehouseId, customer, status, page, pageSize);

            return View(viewModel);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _salesOrderService.GetSalesOrderByIdAsync(id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await _salesOrderService.GetSalesOrderByIdAsync(id);

            if (order == null)
                return NotFound();

            if (order.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending orders can be canceled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var updated = await _salesOrderService.CancelSalesOrderAsync(id);

            TempData[updated ? "SuccessMessage" : "ErrorMessage"] =
                updated ? "Order has been canceled." : "Failed to cancel order.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Create()
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            int warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["ErrorMessage"] = "You haven't been assigned to any warehouse yet.";
                return RedirectToAction("Index", "Home");
            }

            var products = await _salesOrderService.GetProductsForSelectionAsync();
            var warehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);

            ViewBag.Products = products;
            ViewBag.WarehouseId = warehouseId;
            ViewBag.WarehouseName = warehouseName ?? "Unknown";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSalesOrderViewModel model)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            int warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["ErrorMessage"] = "You haven't been assigned to any warehouse yet.";
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                var warehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);

                ViewBag.Products = products;
                ViewBag.WarehouseId = warehouseId;
                ViewBag.WarehouseName = warehouseName ?? "Unknown";

                TempData["ErrorMessage"] = "Please check the order information!";
                return View(model);
            }

            if (model.Details == null || !model.Details.Any())
            {
                TempData["ErrorMessage"] = "Please add at least one product!";

                var products = await _salesOrderService.GetProductsForSelectionAsync();
                var warehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);
                ViewBag.Products = products;
                ViewBag.WarehouseId = warehouseId;
                ViewBag.WarehouseName = warehouseName ?? "Unknown";

                return View(model);
            }

            // Ensure model's warehouse matches user's warehouse
            model.WarehouseId = warehouseId;
            int currentUserId = userId;

            try
            {
                var result =
                    await _salesOrderService.CreateSalesOrderAsync(model, currentUserId);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"System error: {ex.Message}";
            }

            var reloadProducts = await _salesOrderService.GetProductsForSelectionAsync();

            ViewBag.Products = reloadProducts;
            ViewBag.WarehouseId = warehouseId;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CheckInventory(
            [FromBody] InventoryCheckRequest request)
        {
            try
            {
                var result =
                    await _inventoryCheckService
                        .CheckInventoryAvailabilityAsync(request);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new InventoryCheckResult
                {
                    IsValid = false,
                    Message = $"Error checking inventory: {ex.Message}",
                    CheckDetails = new List<InventoryCheckDto>()
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products =
                await _salesOrderService.GetProductsForSelectionAsync();

            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerByPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return Json(new { found = false });

            var result =
                await _salesOrderService.GetCustomerByPhoneAsync(phone.Trim());

            return Json(new
            {
                found = result.Found,
                name = result.Name,
                address = result.Address
            });
        }
    }
}