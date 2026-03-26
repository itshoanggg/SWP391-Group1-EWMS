using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Purchasing Staff")]
    public class PurchaseOrderController : Controller
    {
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly ISupplierService _supplierService;
        private readonly IUserService _userService;
        private readonly IProductRepository _productRepository;

        public PurchaseOrderController(
            IPurchaseOrderService purchaseOrderService,
            ISupplierService supplierService,
            IUserService userService,
            IProductRepository productRepository)
        {
            _purchaseOrderService = purchaseOrderService;
            _supplierService = supplierService;
            _userService = userService;
            _productRepository = productRepository;
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
                TempData["Error"] = "You have not been assigned to any warehouse.";
                return RedirectToAction("Index", "Home");
            }

            var purchaseOrders = await _purchaseOrderService.GetPurchaseOrdersAsync(warehouseId, status);
            ViewBag.CurrentUserId = userId;
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
                TempData["Error"] = "Purchase order not found.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.TotalQuantity = purchaseOrder.PurchaseOrderDetails.Sum(d => d.Quantity);
            ViewBag.TotalAmount = purchaseOrder.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0);
            ViewBag.CurrentUserId = userId;

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
                TempData["Error"] = "You have not been assigned to any warehouse.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Suppliers = new SelectList(
                await _supplierService.GetAllSuppliersAsync(),
                "SupplierId",
                "SupplierName"
            );

            // Load all products for initial dropdown with additional data
            var allProducts = await _productRepository.Context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
            
            ViewBag.AllProducts = allProducts.Select(p => new {
                productId = p.ProductId,
                productName = p.ProductName,
                categoryName = p.Category?.CategoryName ?? "N/A",
                sku = "SKU-" + p.ProductId.ToString().PadLeft(5, '0'),
                costPrice = p.CostPrice ?? 0
            }).ToList();

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
                TempData["Error"] = "Please enter complete information and at least 1 product.";

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

                TempData["Success"] = $"Purchase order PO-{purchaseOrder.PurchaseOrderId:D4} created successfully!";
                return RedirectToAction(nameof(Details), new { id = purchaseOrder.PurchaseOrderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";

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
                    return Json(new { success = false, message = "Unable to update purchase order" });

                return Json(new { success = true, message = "Updated: Goods have arrived at warehouse" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // API: Get Products by Supplier
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            var products = await _purchaseOrderService.GetProductsBySupplierAsync(supplierId);
            return Json(products);
        }

        // API: Get Suppliers by Product
        [HttpGet]
        public async Task<IActionResult> GetSuppliersByProduct(int productId)
        {
            var product = await _productRepository.GetProductByIdAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Product not found" });
            }

            var suppliers = product.ProductSuppliers?
                .Select(ps => new
                {
                    supplierId = ps.SupplierId,
                    supplierName = ps.Supplier?.SupplierName ?? "Unknown"
                })
                .ToList();

            if (suppliers == null || !suppliers.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "No suppliers found for this product"
                });
            }

            return Json(new
            {
                success = true,
                suppliers = suppliers
            });
        }

        // API: Get Supplier Info
        [HttpGet]
        public async Task<IActionResult> GetSupplierInfo(int supplierId)
        {
            var supplier = await _supplierService.GetSupplierByIdAsync(supplierId);
            if (supplier == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                supplierId = supplier.SupplierId,
                supplierName = supplier.SupplierName,
                phone = supplier.Phone ?? "N/A",
                email = supplier.Email ?? "N/A",
                address = supplier.Address ?? "N/A"
            });
        }

        // DELETE: PurchaseOrder/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            var result = await _purchaseOrderService.DeletePurchaseOrderAsync(id, warehouseId, userId);

            if (!result)
                return Json(new { success = false, message = "Unable to delete purchase order. Only the creator can delete this order." });

            return Json(new { success = true, message = "Purchase order deleted successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userService.GetCurrentUserId();
            var warehouseId = await _userService.GetWarehouseIdByUserIdAsync(userId);
            var result = await _purchaseOrderService.CancelPurchaseOrderAsync(id, warehouseId, userId);

            if (!result)
                return Json(new { success = false, message = "Unable to cancel purchase order. Only the creator can cancel this order." });

            return Json(new { success = true, message = "Purchase order cancelled successfully" });
        }

    }
}
