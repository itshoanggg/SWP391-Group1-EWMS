using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

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
        public async Task<IActionResult> Index(string status = "InTransit")
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (warehouseId == 0)
            {
                TempData["Error"] = "Bạn chưa được phân công vào kho nào.";
                return RedirectToAction("Index", "Home");
            }

            await AutoUpdateDeliveredStatus(warehouseId);

            var query = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.CreatedByNavigation)
                .Include(po => po.PurchaseOrderDetails)
                .Where(po =>
                    po.WarehouseId == warehouseId &&
                    po.Status == "InTransit"      // ✅ CHỈ LẤY InTransit
                );

            var purchaseOrders = await query
                .OrderByDescending(po => po.CreatedAt)
                .ToListAsync();

            return View(purchaseOrders);
        }


        // GET: PurchaseOrder/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == GetCurrentUserId())
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.Warehouse)
                .Include(po => po.CreatedByNavigation)
                .Include(po => po.PurchaseOrderDetails)
                    .ThenInclude(pod => pod.Product)
                        .ThenInclude(p => p.Category)
                .Include(po => po.StockInReceipts)
                    .ThenInclude(si => si.StockInDetails)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id
                    && po.WarehouseId == warehouseId);

            if (purchaseOrder == null)
            {
                TempData["Error"] = "Không tìm thấy đơn mua hàng.";
                return RedirectToAction(nameof(Index));
            }

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

            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (warehouseId == 0)
            {
                TempData["Error"] = "Bạn chưa được phân công vào kho nào.";
                return RedirectToAction("Index", "Home");
            }

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
                // ✅ Tạo PO với status = "InTransit"
                var purchaseOrder = new PurchaseOrder
                {
                    SupplierId = model.SupplierId,
                    WarehouseId = warehouseId,
                    CreatedBy = userId,
                    Status = "InTransit", // ✅ Đang vận chuyển
                    CreatedAt = DateTime.Now,
                    ExpectedReceivingDate = model.ExpectedReceivingDate
                };

                _context.PurchaseOrders.Add(purchaseOrder);
                await _context.SaveChangesAsync();

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

                TempData["Success"] = $"Tạo đơn mua hàng PO-{purchaseOrder.PurchaseOrderId:D4} thành công!";
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

        // POST: PurchaseOrder/MarkAsDelivered - Đánh dấu hàng đã về kho
        [HttpPost]
        public async Task<IActionResult> MarkAsDelivered(int id)
        {
            try
            {
                var warehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == GetCurrentUserId())
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .FirstOrDefaultAsync(po => po.PurchaseOrderId == id
                        && po.WarehouseId == warehouseId);

                if (purchaseOrder == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                if (purchaseOrder.Status != "InTransit")
                {
                    return Json(new { success = false, message = "Chỉ có thể cập nhật đơn hàng đang vận chuyển" });
                }

                purchaseOrder.Status = "Delivered";
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã cập nhật: Hàng đã về kho" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // API: Get Products by Supplier
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
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
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == GetCurrentUserId())
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderDetails)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id
                    && po.WarehouseId == warehouseId);

            if (purchaseOrder == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
            }

            // Chỉ cho phép xóa đơn InTransit
            if (purchaseOrder.Status != "InTransit")
            {
                return Json(new { success = false, message = "Chỉ có thể xóa đơn hàng đang vận chuyển" });
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

        private int GetCurrentUserId()
        {
            return 4; // TODO: Replace with actual auth
        }


        // ✅ Tự động cập nhật trạng thái hàng đã về kho
        private async Task AutoUpdateDeliveredStatus(int warehouseId)
        {
            var today = DateTime.Today;

            var ordersToUpdate = await _context.PurchaseOrders
                .Where(po =>
                    po.WarehouseId == warehouseId &&
                    po.Status == "InTransit" &&
                    po.ExpectedReceivingDate.HasValue &&
                    po.ExpectedReceivingDate.Value.Date <= today)
                .ToListAsync();

            if (ordersToUpdate.Any())
            {
                foreach (var po in ordersToUpdate)
                {
                    po.Status = "Delivered";
                }

                await _context.SaveChangesAsync();
            }
        }
    }


    // ViewModels
    public class PurchaseOrderCreateViewModel
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày nhận hàng dự kiến")]
        [Display(Name = "Ngày nhận hàng dự kiến")]
        public DateTime? ExpectedReceivingDate { get; set; }

        public List<PurchaseOrderDetailViewModel> Details { get; set; } = new List<PurchaseOrderDetailViewModel>();
    }

    public class PurchaseOrderDetailViewModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}