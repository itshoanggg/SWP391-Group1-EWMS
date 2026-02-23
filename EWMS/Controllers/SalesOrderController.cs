using EWMS.DTOs;
using EWMS.Services;
using EWMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Staff")]
    public class SalesOrderController : Controller
    {
        private readonly ISalesOrderService _salesOrderService;
        private readonly IInventoryCheckService _inventoryCheckService;
        private readonly ILogger<SalesOrderController> _logger;

        public SalesOrderController(
            ISalesOrderService salesOrderService,
            IInventoryCheckService inventoryCheckService,
            ILogger<SalesOrderController> logger)
        {
            _salesOrderService = salesOrderService;
            _inventoryCheckService = inventoryCheckService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        }

        private int GetCurrentWarehouseId()
        {
            return int.Parse(User.FindFirst("WarehouseId")!.Value);
        }

        // GET: SalesOrder
        public async Task<IActionResult> Index()
        {
            int warehouseId = GetCurrentWarehouseId();
            var viewModel = await _salesOrderService.GetSalesOrdersByWarehouseAsync(warehouseId);
            return View(viewModel);
        }

        // GET: SalesOrder/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _salesOrderService.GetSalesOrderByIdAsync(id);
            if (order == null)
                return NotFound();

            return View(order);
        }

        // GET: SalesOrder/Create
        public async Task<IActionResult> Create()
        {
            var products = await _salesOrderService.GetProductsForSelectionAsync();

            ViewBag.Products = products;
            ViewBag.WarehouseId = GetCurrentWarehouseId();
            ViewBag.WarehouseName = User.FindFirst("WarehouseName")?.Value;

            return View();
        }

        // POST: SalesOrder/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSalesOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = GetCurrentWarehouseId();
                ViewBag.WarehouseName = User.FindFirst("WarehouseName")?.Value;
                return View(model);
            }

            if (model.Details == null || !model.Details.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng thêm ít nhất một sản phẩm!";
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = GetCurrentWarehouseId();
                ViewBag.WarehouseName = User.FindFirst("WarehouseName")?.Value;
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            model.WarehouseId = GetCurrentWarehouseId();

            try
            {
                var result = await _salesOrderService.CreateSalesOrderAsync(model, currentUserId);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = result.Message;
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = GetCurrentWarehouseId();
                ViewBag.WarehouseName = User.FindFirst("WarehouseName")?.Value;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sales order");
                TempData["ErrorMessage"] = "Lỗi hệ thống!";
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = GetCurrentWarehouseId();
                ViewBag.WarehouseName = User.FindFirst("WarehouseName")?.Value;
                return View(model);
            }
        }

        // POST: SalesOrder/CheckInventory
        [HttpPost]
        public async Task<IActionResult> CheckInventory([FromBody] InventoryCheckRequest request)
        {
            try
            {
                var result = await _inventoryCheckService.CheckInventoryAvailabilityAsync(request);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking inventory");
                return Json(new InventoryCheckResult
                {
                    IsValid = false,
                    Message = "Lỗi kiểm tra tồn kho",
                    CheckDetails = new List<InventoryCheckDto>()
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _salesOrderService.GetProductsForSelectionAsync();
            return Json(products);
        }
    }
}