using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EWMS.Controllers
{
    public class StockInController : Controller
    {
        private readonly EWMSContext _context;

        public StockInController(EWMSContext context)
        {
            _context = context;
        }

        // GET: StockIn/Index - Danh sách Purchase Orders
        public async Task<IActionResult> Index()
        {
            var userId = 5;
            //var userId = GetCurrentUserId();
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

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;
            return View();
        }

        // GET: StockIn/GoodsReceipt/{id} - Chi tiết nhập kho
        public async Task<IActionResult> Details(int id)
        {
            var userId = 5;
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.Warehouse)
                .Include(po => po.CreatedByNavigation)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id);

            if (purchaseOrder == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index");
            }

            // Kiểm tra quyền truy cập
            var hasAccess = await _context.UserWarehouses
                .AnyAsync(uw => uw.UserId == userId && uw.WarehouseId == purchaseOrder.WarehouseId);

            if (!hasAccess)
            {
                TempData["Error"] = "Bạn không có quyền xem đơn hàng này.";
                return RedirectToAction("Index");
            }

            ViewBag.PurchaseOrderId = id;
            ViewBag.UserId = userId;
            ViewBag.WarehouseId = purchaseOrder.WarehouseId;

            return View(purchaseOrder);
        }

        // API: Get Purchase Orders
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrders(
    int warehouseId,
    string status = "",
    string search = ""
)
        {
            try
            {
                var query =
                    from po in _context.PurchaseOrders
                        .Include(p => p.Supplier)
                        .Include(p => p.CreatedByNavigation)
                    join pod in _context.PurchaseOrderDetails
                        on po.PurchaseOrderId equals pod.PurchaseOrderId into podGroup
                    where po.WarehouseId == warehouseId
                    select new
                    {
                        po,
                        podGroup
                    };

                // Filter theo status
                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    query = query.Where(x => x.po.Status == status);
                }

                // Search
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(x =>
                        x.po.PurchaseOrderId.ToString() == search ||
                        x.po.Supplier.SupplierName.Contains(search)
                    );
                }

                var result = await query
                .OrderByDescending(x => x.po.PurchaseOrderId)
                .Select(x => new
                {
                    purchaseOrderId = x.po.PurchaseOrderId,
                    supplierName = x.po.Supplier.SupplierName,
                    status = x.po.Status,

                    // Tổng số lượng item
                    totalItems = x.podGroup
                        .Sum(d => (int?)d.Quantity) ?? 0,

                    // Tổng tiền: SUM LineTotal (TotalPrice)
                    totalAmount = x.podGroup
                        .Sum(d => (decimal?)d.TotalPrice) ?? 0,

                    createdBy = x.po.CreatedByNavigation.FullName
                                ?? x.po.CreatedByNavigation.Username
                })
                .ToListAsync();
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    error = "Load Purchase Orders failed",
                    detail = ex.Message
                });
            }
        }


        // API: Get Purchase Order Info
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderInfo(int purchaseOrderId)
        {
            try
            {
                var po = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Warehouse)
                    .Include(p => p.CreatedByNavigation)
                    .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId);

                if (po == null)
                    return Json(new { error = "Không tìm thấy đơn hàng" });

                // Kiểm tra đã nhập kho chưa
                var stockIn = await _context.StockInReceipts
                    .FirstOrDefaultAsync(si => si.PurchaseOrderId == purchaseOrderId);

                return Json(new
                {
                    purchaseOrderId = po.PurchaseOrderId,
                    supplierName = po.Supplier.SupplierName,
                    supplierContact = po.Supplier.ContactPerson,
                    supplierPhone = po.Supplier.Phone,
                    supplierAddress = po.Supplier.Address,
                    warehouseName = po.Warehouse.WarehouseName,
                    status = po.Status,
                    createdBy = po.CreatedByNavigation.FullName ?? po.CreatedByNavigation.Username,
                    hasStockIn = stockIn != null,
                    stockInId = stockIn?.StockInId
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Purchase Order Products
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderProducts(int purchaseOrderId)
        {
            try
            {
                var products = await _context.PurchaseOrderDetails
                    .Include(pod => pod.Product)
                    .ThenInclude(p => p.Category)
                    .Where(pod => pod.PurchaseOrderId == purchaseOrderId)
                    .Select(pod => new
                    {
                        productId = pod.ProductId,
                        sku = "SKU-" + pod.ProductId.ToString().PadLeft(5, '0'),
                        productName = pod.Product.ProductName,
                        categoryName = pod.Product.Category.CategoryName,
                        unit = pod.Product.Unit,
                        orderedQty = pod.Quantity,
                        unitPrice = pod.UnitPrice
                    })
                    .ToListAsync();

                return Json(products);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Available Locations
        [HttpGet]
        public async Task<IActionResult> GetAvailableLocations(int warehouseId)
        {
            var locations = await _context.Locations
                .Where(l => l.WarehouseId == warehouseId)
                .Select(l => new
                {
                    l.LocationId,
                    l.LocationCode,
                    l.LocationName,
                    l.Rack,
                    maxCapacity = l.Capacity, // ví dụ 200
                    currentStock = _context.Inventories
                        .Where(i => i.LocationId == l.LocationId)
                        .Sum(i => (int?)i.Quantity) ?? 0
                })
                .ToListAsync();

            return Json(locations);
        }


        [HttpGet]
        public async Task<IActionResult> CheckLocationCapacity(int locationId)
        {
            var maxCapacity = await _context.Locations
                .Where(l => l.LocationId == locationId)
                .Select(l => l.Capacity)
                .FirstOrDefaultAsync();

            var currentStock = await _context.Inventories
                .Where(i => i.LocationId == locationId)
                .SumAsync(i => (int?)i.Quantity) ?? 0;

            return Json(new
            {
                maxCapacity,
                currentStock,
                availableCapacity = maxCapacity - currentStock
            });
        }






        // API: Confirm Stock In (Xác nhận nhập kho)
        [HttpPost]
        public async Task<IActionResult> ConfirmStockIn([FromBody] StockInRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = 5;

                // Tạo Stock In Receipt
                var stockInReceipt = new StockInReceipt
                {
                    PurchaseOrderId = request.PurchaseOrderId,
                    WarehouseId = request.WarehouseId,
                    ReceivedBy = userId,
                    ReceivedDate = DateTime.Now,
                    Reason = "Purchase",
                    TotalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice)
                };

                _context.StockInReceipts.Add(stockInReceipt);
                await _context.SaveChangesAsync();

                // Tạo Stock In Details
                foreach (var item in request.Items)
                {
                    var stockInDetail = new StockInDetail
                    {
                        StockInId = stockInReceipt.StockInId,
                        ProductId = item.ProductId,
                        LocationId = item.LocationId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    };

                    _context.StockInDetails.Add(stockInDetail);

                    // Cập nhật Inventory
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(inv => inv.LocationId == item.LocationId
                            && inv.ProductId == item.ProductId);

                    if (inventory != null)
                    {
                        inventory.Quantity += item.Quantity;
                    }
                    else
                    {
                        inventory = new Inventory
                        {
                            ProductId = item.ProductId,
                            LocationId = item.LocationId,
                            Quantity = item.Quantity
                        };
                        _context.Inventories.Add(inventory);
                    }
                }

                await _context.SaveChangesAsync();

                // Cập nhật Status của Purchase Order
                var purchaseOrder = await _context.PurchaseOrders
                    .FindAsync(request.PurchaseOrderId);

                if (purchaseOrder != null)
                {
                    purchaseOrder.Status = "Received";
                    await _context.SaveChangesAsync();
                }

                // Log activity
                var activityLog = new ActivityLog
                {
                    UserId = userId,
                    Action = "Received",
                    TableName = "StockInReceipts",
                    RecordId = stockInReceipt.StockInId,
                    Description = $"Nhập kho PO-{request.PurchaseOrderId}"
                };
                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    stockInId = stockInReceipt.StockInId,
                    message = "Nhập kho thành công!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, error = ex.Message });
            }
        }

        // API: Get Statistics
        [HttpGet]
        public async Task<IActionResult> GetStatistics(int warehouseId)
        {
            try
            {
                var totalOrders = await _context.PurchaseOrders
                    .Where(po => po.WarehouseId == warehouseId)
                    .CountAsync();

                var pendingOrders = await _context.PurchaseOrders
                    .Where(po => po.WarehouseId == warehouseId && po.Status == "Pending")
                    .CountAsync();

                var approvedOrders = await _context.PurchaseOrders
                    .Where(po => po.WarehouseId == warehouseId && po.Status == "Approved")
                    .CountAsync();

                var receivedOrders = await _context.PurchaseOrders
                    .Where(po => po.WarehouseId == warehouseId && po.Status == "Received")
                    .CountAsync();

                return Json(new
                {
                    totalOrders,
                    pendingOrders,
                    approvedOrders,
                    receivedOrders
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private int GetCurrentUserId()
        {
            return 7; // TODO: Replace with actual authentication
        }
    }

    // Request Models
    public class StockInRequest
    {
        public int PurchaseOrderId { get; set; }
        public int WarehouseId { get; set; }
        public List<StockInItem> Items { get; set; }
    }

    public class StockInItem
    {
        public int ProductId { get; set; }
        public int LocationId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}