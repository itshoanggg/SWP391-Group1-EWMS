using EWMS.Services;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Admin,Inventory Staff,Warehouse Manager,Sales Staff")]
    public class StockOutReceiptController : Controller
    {
        private readonly IStockOutReceiptService _stockOutReceiptService;
        private readonly IUserService _userService;
        private readonly TransferService _transferService;

        public StockOutReceiptController(
            IStockOutReceiptService stockOutReceiptService,
            IUserService userService,
            TransferService transferService)
        {
            _stockOutReceiptService = stockOutReceiptService;
            _userService = userService;
            _transferService = transferService;
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
            var viewModel =
                await _stockOutReceiptService.GetPendingOrdersForIndexAsync(
                    warehouseId, customer, status, page, pageSize);

            ViewBag.WarehouseId = warehouseId;

            return View(viewModel);
        }

        public async Task<IActionResult> History(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? customer,
            string? issuedBy,
            int page = 1)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            int warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["ErrorMessage"] = "You haven't been assigned to any warehouse yet.";
                return RedirectToAction("Index", "Home");
            }

            var vm =
                await _stockOutReceiptService.GetStockOutReceiptsByWarehouseAsync(warehouseId);

            IEnumerable<StockOutReceiptViewModel> filtered = vm.Receipts;

            if (dateFrom.HasValue)
            {
                vm.DateFrom = dateFrom.Value.Date;
                filtered = filtered.Where(r =>
                    r.IssuedDate.HasValue &&
                    r.IssuedDate.Value >= vm.DateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                var toExclusive = dateTo.Value.Date.AddDays(1);
                vm.DateTo = dateTo.Value.Date;

                filtered = filtered.Where(r =>
                    r.IssuedDate.HasValue &&
                    r.IssuedDate.Value < toExclusive);
            }

            if (!string.IsNullOrWhiteSpace(customer))
            {
                var lc = customer.Trim().ToLower();
                vm.FilterCustomer = customer;

                filtered = filtered.Where(r =>
                    (!string.IsNullOrEmpty(r.CustomerName) &&
                     r.CustomerName.ToLower().Contains(lc))
                    ||
                    (!string.IsNullOrEmpty(r.CustomerPhone) &&
                     r.CustomerPhone.ToLower().Contains(lc)));
            }

            if (!string.IsNullOrWhiteSpace(issuedBy))
            {
                var li = issuedBy.Trim().ToLower();
                vm.FilterIssuedBy = issuedBy;

                filtered = filtered.Where(r =>
                    !string.IsNullOrEmpty(r.IssuedByName) &&
                    r.IssuedByName.ToLower().Contains(li));
            }

            const int pageSize = 5;

            vm.PageSize = pageSize;
            vm.TotalCount = filtered.Count();
            vm.TotalPages =
                (int)Math.Ceiling(vm.TotalCount / (double)pageSize);

            vm.Page = page < 1 ? 1 : page;
            if (vm.TotalPages > 0 && vm.Page > vm.TotalPages)
                vm.Page = vm.TotalPages;

            vm.Receipts = filtered
                .OrderByDescending(r => r.IssuedDate ?? r.CreatedAt)
                .Skip((vm.Page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(vm);
        }

        public async Task<IActionResult> Details(int id)
        {
            var receipt =
                await _stockOutReceiptService.GetStockOutReceiptByIdAsync(id);

            if (receipt == null)
                return NotFound();

            return View(receipt);
        }

        public async Task<IActionResult> Create(int orderId, string? orderType = null)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            int warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["ErrorMessage"] = "You haven't been assigned to any warehouse yet.";
                return RedirectToAction(nameof(Index));
            }

            var warehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);
            ViewBag.WarehouseId = warehouseId;
            ViewBag.WarehouseName = warehouseName ?? "Unknown";

            // Handle transfer orders
            if (orderType == "transfer")
            {
                try
                {
                    var transferModel = await _transferService.GetTransferForStockOutAsync(orderId, warehouseId);
                    if (transferModel == null)
                    {
                        TempData["ErrorMessage"] = "Transfer not found or cannot be processed.";
                        return RedirectToAction(nameof(Index));
                    }
                    
                    ViewBag.OrderType = "transfer";
                    ViewBag.IsTransfer = true;
                    return View(transferModel);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                    return RedirectToAction(nameof(Index));
                }
            }

            // Handle sales orders
            var order = await _stockOutReceiptService.GetSalesOrderForStockOutAsync(orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.OrderType = "sales";
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTransfer(int transferId)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            int warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
            {
                TempData["ErrorMessage"] = "You haven't been assigned to any warehouse yet.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var model = await _transferService.GetTransferStockOutAsync(transferId, warehouseId);
                if (model == null)
                {
                    TempData["ErrorMessage"] = "Transfer not found or cannot be processed.";
                    return RedirectToAction(nameof(Index));
                }

                return View("CreateTransfer", model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateStockOutReceiptViewModel model, string? orderType = null)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");
            
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please check the stock out receipt information!";
                var warehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);
                ViewBag.WarehouseId = model.WarehouseId;
                ViewBag.WarehouseName = warehouseName ?? "Unknown";
                ViewBag.OrderType = orderType ?? "sales";

                if (orderType == "transfer")
                {
                    var transfer = await _transferService.GetTransferForStockOutAsync(model.SalesOrderId, model.WarehouseId);
                    return View(transfer);
                }
                
                var order = await _stockOutReceiptService.GetSalesOrderForStockOutAsync(model.SalesOrderId);
                return View(order);
            }

            if (model.Details == null || !model.Details.Any())
            {
                TempData["ErrorMessage"] = "Please select pickup location for all products!";
                var warehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);
                ViewBag.WarehouseId = model.WarehouseId;
                ViewBag.WarehouseName = warehouseName ?? "Unknown";
                ViewBag.OrderType = orderType ?? "sales";

                if (orderType == "transfer")
                {
                    var transfer = await _transferService.GetTransferForStockOutAsync(model.SalesOrderId, model.WarehouseId);
                    return View(transfer);
                }

                var order = await _stockOutReceiptService.GetSalesOrderForStockOutAsync(model.SalesOrderId);
                return View(order);
            }

            try
            {
                // Handle transfer orders
                if (orderType == "transfer")
                {
                    var transferModel = new CreateTransferStockOutViewModel
                    {
                        TransferId = model.SalesOrderId,
                        WarehouseId = model.WarehouseId,
                        IssuedDate = model.IssuedDate,
                        Details = model.Details
                    };
                    
                    var receiptId = await _transferService.ProcessTransferStockOutAsync(transferModel, userId);
                    TempData["SuccessMessage"] = "Transfer stock-out processed successfully.";
                    return RedirectToAction(nameof(Details), new { id = receiptId });
                }

                // Handle sales orders
                var result = await _stockOutReceiptService.CreateStockOutReceiptAsync(model, userId);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Details), new { id = result.ReceiptId });
                }

                TempData["ErrorMessage"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"System error: {ex.Message}";
            }

            int retryWarehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            var retryWarehouseName = await _userService.GetWarehouseNameByUserIdAsync(userId);
            ViewBag.WarehouseId = retryWarehouseId;
            ViewBag.WarehouseName = retryWarehouseName ?? "Unknown";
            ViewBag.OrderType = orderType ?? "sales";

            if (orderType == "transfer")
            {
                var retryTransfer = await _transferService.GetTransferForStockOutAsync(model.SalesOrderId, model.WarehouseId);
                return View(retryTransfer);
            }

            var retryOrder = await _stockOutReceiptService.GetSalesOrderForStockOutAsync(model.SalesOrderId);
            return View(retryOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTransfer(CreateTransferStockOutViewModel model)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid || model.Details == null || !model.Details.Any())
            {
                TempData["ErrorMessage"] = "Please select pickup location for all transfer items.";
                var retryModel = await _transferService.GetTransferStockOutAsync(model.TransferId, model.WarehouseId);
                return View("CreateTransfer", retryModel);
            }

            try
            {
                var receiptId = await _transferService.ProcessTransferStockOutAsync(model, userId);
                TempData["SuccessMessage"] = "Transfer stock-out processed successfully.";
                return RedirectToAction(nameof(Details), new { id = receiptId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                var retryModel = await _transferService.GetTransferStockOutAsync(model.TransferId, model.WarehouseId);
                return View("CreateTransfer", retryModel);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableLocations(int warehouseId, int productId)
        {
            var locations =
                await _stockOutReceiptService.GetAvailableLocationsForProductAsync(
                    warehouseId, productId);

            return Json(locations);
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesOrderDetails(int orderId)
        {
            var order =
                await _stockOutReceiptService.GetSalesOrderForStockOutAsync(orderId);

            if (order == null)
                return Json(new { success = false, message = "Order not found!" });

            return Json(new { success = true, order });
        }
    }
}
