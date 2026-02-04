using EWMS.DTOs;
using EWMS.Services;
using EWMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EWMS.Controllers
{
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

        // GET: SalesOrder
        public async Task<IActionResult> Index()
        {
            // TODO: Get warehouse ID from current logged-in user
            // For now, hardcode warehouse ID = 1 (Hanoi Warehouse)
            int warehouseId = 1;

            var viewModel = await _salesOrderService.GetSalesOrdersByWarehouseAsync(warehouseId);
            return View(viewModel);
        }

        // GET: SalesOrder/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _salesOrderService.GetSalesOrderByIdAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: SalesOrder/Create
        public async Task<IActionResult> Create()
        {
            var products = await _salesOrderService.GetProductsForSelectionAsync();
            ViewBag.Products = products;

            // TODO: Get warehouse ID from current logged-in user
            ViewBag.WarehouseId = 1;
            ViewBag.WarehouseName = "Hanoi Warehouse";

            return View();
        }

        // POST: SalesOrder/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSalesOrderViewModel model)
        {
            // DEBUG LOGGING
            _logger.LogInformation("=== CREATE SALES ORDER DEBUG ===");
            _logger.LogInformation($"CustomerName: {model.CustomerName}");
            _logger.LogInformation($"WarehouseId: {model.WarehouseId}");
            _logger.LogInformation($"ExpectedDeliveryDate: {model.ExpectedDeliveryDate}");
            _logger.LogInformation($"Details Count: {model.Details?.Count ?? 0}");

            if (model.Details != null && model.Details.Any())
            {
                foreach (var detail in model.Details)
                {
                    _logger.LogInformation($"  Product {detail.ProductId}: Qty={detail.Quantity}, Price={detail.UnitPrice}");
                }
            }
            else
            {
                _logger.LogWarning("Details is null or empty!");
            }

            // Check ModelState
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid:");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Any())
                    {
                        _logger.LogWarning($"  Key: {error.Key}");
                        foreach (var err in error.Value.Errors)
                        {
                            _logger.LogWarning($"    - {err.ErrorMessage}");
                        }
                    }
                }

                // Return view with errors
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = model.WarehouseId;
                ViewBag.WarehouseName = "Hanoi Warehouse";
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin đơn hàng!";
                return View(model);
            }

            // Additional validation
            if (model.Details == null || !model.Details.Any())
            {
                _logger.LogError("No product details provided");
                TempData["ErrorMessage"] = "Vui lòng thêm ít nhất một sản phẩm!";
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = model.WarehouseId;
                ViewBag.WarehouseName = "Hanoi Warehouse";
                return View(model);
            }

            // TODO: Get current user ID from authentication
            int currentUserId = 3; // Sales Staff 1

            try
            {
                _logger.LogInformation("Attempting to create sales order...");
                var result = await _salesOrderService.CreateSalesOrderAsync(model, currentUserId);

                if (result.Success)
                {
                    _logger.LogInformation($"Sales order created successfully. OrderId: {result.OrderId}");
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    _logger.LogWarning($"Failed to create sales order: {result.Message}");
                    TempData["ErrorMessage"] = result.Message;
                    var products = await _salesOrderService.GetProductsForSelectionAsync();
                    ViewBag.Products = products;
                    ViewBag.WarehouseId = model.WarehouseId;
                    ViewBag.WarehouseName = "Hanoi Warehouse";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while creating sales order");
                TempData["ErrorMessage"] = $"Lỗi hệ thống: {ex.Message}";
                var products = await _salesOrderService.GetProductsForSelectionAsync();
                ViewBag.Products = products;
                ViewBag.WarehouseId = model.WarehouseId;
                ViewBag.WarehouseName = "Hanoi Warehouse";
                return View(model);
            }
        }

        // POST: SalesOrder/CheckInventory
        [HttpPost]
        public async Task<IActionResult> CheckInventory([FromBody] InventoryCheckRequest request)
        {
            try
            {
                _logger.LogInformation($"Checking inventory for {request.Products.Count} products");
                var result = await _inventoryCheckService.CheckInventoryAvailabilityAsync(request);
                _logger.LogInformation($"Inventory check result: IsValid={result.IsValid}");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking inventory");
                return Json(new InventoryCheckResult
                {
                    IsValid = false,
                    Message = $"Lỗi khi kiểm tra tồn kho: {ex.Message}",
                    CheckDetails = new List<InventoryCheckDto>()
                });
            }
        }

        // GET: API endpoint to get products
        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _salesOrderService.GetProductsForSelectionAsync();
            return Json(products);
        }
    }
}