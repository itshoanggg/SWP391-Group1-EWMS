using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EWMS.Controllers
{
    public class PurchaseOrderController : Controller
    {
        private readonly EWMSContext _context;

        public PurchaseOrderController(EWMSContext context)
        {
            _context = context;
        }

        // GET: PurchaseOrder/Index
        public async Task<IActionResult> Index(string status = "")
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy WarehouseID của user
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (warehouseId == 0)
            {
                TempData["Error"] = "Bạn chưa được phân công vào kho nào.";
                return RedirectToAction("Index", "Home");
            }

            // Query đơn mua hàng
            var query = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.CreatedByNavigation)
                .Include(po => po.PurchaseOrderDetails)
                .Where(po => po.WarehouseId == warehouseId);

            // Filter theo status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(po => po.Status == status);
            }

            var purchaseOrders = await query
                .OrderByDescending(po => po.CreatedAt)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.WarehouseId = warehouseId;

            return View(purchaseOrders);
        }

        // GET: PurchaseOrder/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.Warehouse)
                .Include(po => po.CreatedByNavigation)
                .Include(po => po.PurchaseOrderDetails)
                    .ThenInclude(pod => pod.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id);

            if (purchaseOrder == null)
            {
                TempData["Error"] = "Không tìm thấy đơn mua hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Tính tổng
            ViewBag.TotalQuantity = purchaseOrder.PurchaseOrderDetails.Sum(d => d.Quantity);
            ViewBag.TotalAmount = purchaseOrder.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0);

            return View(purchaseOrder);
        }

        // GET: PurchaseOrder/Create
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy WarehouseID của user
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (warehouseId == 0)
            {
                TempData["Error"] = "Bạn chưa được phân công vào kho nào.";
                return RedirectToAction("Index", "Home");
            }

            // Load danh sách nhà cung cấp
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.OrderBy(s => s.SupplierName).ToListAsync(),
                "SupplierId",
                "SupplierName"
            );

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;

            return View();
        }

        // POST: PurchaseOrder/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (!ModelState.IsValid || model.Details == null || !model.Details.Any())
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin và ít nhất 1 sản phẩm.";

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.OrderBy(s => s.SupplierName).ToListAsync(),
                    "SupplierId",
                    "SupplierName"
                );
                ViewBag.WarehouseId = warehouseId;
                ViewBag.UserId = userId;

                return View(model);
            }

            try
            {
                // Tạo Purchase Order
                var purchaseOrder = new PurchaseOrder
                {
                    SupplierId = model.SupplierId,
                    WarehouseId = warehouseId,
                    CreatedBy = userId,
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                _context.PurchaseOrders.Add(purchaseOrder);
                await _context.SaveChangesAsync();

                // Tạo Purchase Order Details
                foreach (var detail in model.Details)
                {
                    if (detail.ProductId > 0 && detail.Quantity > 0 && detail.UnitPrice > 0)
                    {
                        var orderDetail = new PurchaseOrderDetail
                        {
                            PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                            ProductId = detail.ProductId,
                            Quantity = detail.Quantity,
                            UnitPrice = detail.UnitPrice
                        };

                        _context.PurchaseOrderDetails.Add(orderDetail);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Tạo đơn mua hàng {purchaseOrder.PurchaseOrderId} thành công!";
                return RedirectToAction(nameof(Details), new { id = purchaseOrder.PurchaseOrderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";

                ViewBag.Suppliers = new SelectList(
                    await _context.Suppliers.OrderBy(s => s.SupplierName).ToListAsync(),
                    "SupplierId",
                    "SupplierName"
                );
                ViewBag.WarehouseId = warehouseId;
                ViewBag.UserId = userId;

                return View(model);
            }
        }

        // POST: PurchaseOrder/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var purchaseOrder = await _context.PurchaseOrders.FindAsync(id);

            if (purchaseOrder == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
            }

            purchaseOrder.Status = status;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật trạng thái thành công" });
        }

        // API: Get Products by Supplier
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            // Lấy tất cả sản phẩm (hoặc có thể filter theo supplier nếu có relationship)
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId != null)
                .OrderBy(p => p.ProductName)
                .Select(p => new
                {
                    productId = p.ProductId,
                    productName = p.ProductName,
                    categoryName = p.Category.CategoryName,
                    costPrice = p.CostPrice ?? 0
                })
                .ToListAsync();

            return Json(products);
        }

        // DELETE: PurchaseOrder/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderDetails)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id);

            if (purchaseOrder == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
            }

            // Chỉ cho phép xóa đơn có status = "Pending"
            if (purchaseOrder.Status != "Pending")
            {
                return Json(new { success = false, message = "Chỉ có thể xóa đơn hàng ở trạng thái Pending" });
            }

            try
            {
                _context.PurchaseOrders.Remove(purchaseOrder);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa đơn hàng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Helper method
        private int GetCurrentUserId()
        {
            return 4;
        }
    }

    // ViewModel for Create
    public class PurchaseOrderCreateViewModel
    {
        public int SupplierId { get; set; }
        public List<PurchaseOrderDetailViewModel> Details { get; set; } = new List<PurchaseOrderDetailViewModel>();
    }

    public class PurchaseOrderDetailViewModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}