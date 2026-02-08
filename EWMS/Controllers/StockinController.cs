using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // GET: StockIn/Index - Hiển thị danh sách PO để nhập kho
        public async Task<IActionResult> Index()
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

            // ✅ TỰ ĐỘNG CẬP NHẬT HÀNG ĐÃ VỀ
            await AutoUpdateDeliveredStatus(warehouseId);

            ViewBag.WarehouseId = warehouseId;

            return View();
        }

        // API: Get Purchase Orders (cho Index page)
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrders(int warehouseId, string status = "", string search = "")
        {
            try
            {
                var userWarehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == GetCurrentUserId())
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                if (userWarehouseId != warehouseId)
                {
                    return Json(new { error = "Bạn không có quyền truy cập kho này" });
                }

                // ✅ TỰ ĐỘNG CẬP NHẬT TRẠNG THÁI HÀNG ĐÃ VỀ
                await AutoUpdateDeliveredStatus(warehouseId);

                var query = _context.PurchaseOrders
                    .Include(po => po.Supplier)
                    .Include(po => po.CreatedByNavigation)
                    .Include(po => po.PurchaseOrderDetails)
                    .Include(po => po.StockInReceipts)
                        .ThenInclude(si => si.StockInDetails)
                    .Where(po => po.WarehouseId == warehouseId);

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(po => po.Status == status);
                }

                // Filter by search
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(po =>
                        po.PurchaseOrderId.ToString().Contains(search) ||
                        po.Supplier.SupplierName.Contains(search)
                    );
                }

                var purchaseOrders = await query
                    .OrderByDescending(po => po.CreatedAt)
                    .ToListAsync();

                var result = purchaseOrders.Select(po =>
                {
                    var totalItems = po.PurchaseOrderDetails.Sum(d => d.Quantity);
                    var receivedItems = po.StockInReceipts
                        .SelectMany(si => si.StockInDetails)
                        .Sum(sid => sid.Quantity);
                    var remainingItems = totalItems - receivedItems;

                    return new
                    {
                        purchaseOrderId = po.PurchaseOrderId,
                        supplierName = po.Supplier.SupplierName,
                        expectedReceivingDate = po.ExpectedReceivingDate,
                        totalItems = totalItems,
                        receivedItems = receivedItems,
                        remainingItems = remainingItems,
                        totalAmount = po.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0),
                        createdBy = po.CreatedByNavigation.FullName ?? po.CreatedByNavigation.Username,
                        status = po.Status,
                        createdAt = po.CreatedAt
                    };
                }).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPurchaseOrders: {ex.Message}");
                return Json(new { error = $"Lỗi server: {ex.Message}" });
            }
        }

        // GET: StockIn/Details/poId - Xem chi tiết PO để nhập kho
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
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

            // Kiểm tra status - chỉ cho xem Delivered hoặc PartiallyReceived
            if (purchaseOrder.Status != "Delivered" && purchaseOrder.Status != "PartiallyReceived")
            {
                TempData["Error"] = "Chỉ có thể nhập kho cho đơn hàng đã về kho.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;

            return View(purchaseOrder);
        }

        // API: Get Purchase Order Info
        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderInfo(int purchaseOrderId)
        {
            try
            {
                var warehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == GetCurrentUserId())
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Supplier)
                    .Include(po => po.CreatedByNavigation)
                    .Include(po => po.StockInReceipts)
                    .FirstOrDefaultAsync(po => po.PurchaseOrderId == purchaseOrderId
                        && po.WarehouseId == warehouseId);

                if (purchaseOrder == null)
                {
                    return Json(new { error = "Không tìm thấy đơn hàng" });
                }
                var isFullyReceived = purchaseOrder.Status == "Received";

                var result = new
                {
                    purchaseOrderId = purchaseOrder.PurchaseOrderId,
                    supplierName = purchaseOrder.Supplier.SupplierName,
                    supplierId = purchaseOrder.Supplier.SupplierId,
                    supplierPhone = purchaseOrder.Supplier.Phone,
                    createdBy = purchaseOrder.CreatedByNavigation?.FullName ?? purchaseOrder.CreatedByNavigation?.Username,
                    createdAt = purchaseOrder.CreatedAt,
                    hasStockIn = isFullyReceived
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrderProducts(int purchaseOrderId)
        {
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == GetCurrentUserId())
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderDetails)
                    .ThenInclude(pod => pod.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(po =>
                    po.PurchaseOrderId == purchaseOrderId &&
                    po.WarehouseId == warehouseId);

            if (purchaseOrder == null)
                return Json(new { error = "Không tìm thấy đơn hàng" });

            // ✅ Tổng số đã nhập theo ProductId
            var receivedMap = await _context.StockInReceipts
                .Where(sir => sir.PurchaseOrderId == purchaseOrderId)
                .SelectMany(sir => sir.StockInDetails)
                .GroupBy(sid => sid.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ReceivedQty = g.Sum(x => x.Quantity)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.ReceivedQty);

            var products = purchaseOrder.PurchaseOrderDetails.Select(pod =>
            {
                var receivedQty = receivedMap.ContainsKey(pod.ProductId)
                    ? receivedMap[pod.ProductId]
                    : 0;

                var remainingQty = pod.Quantity - receivedQty;

                return new
                {
                    productId = pod.ProductId,
                    sku = $"SKU-{pod.ProductId:D5}",
                    productName = pod.Product.ProductName,
                    categoryName = pod.Product.Category?.CategoryName ?? "N/A",
                    orderedQty = pod.Quantity,
                    receivedQty = receivedQty,
                    remainingQty = remainingQty < 0 ? 0 : remainingQty,
                    unitPrice = pod.UnitPrice
                };
            }).ToList();

            return Json(products);
        }


        // GET: StockIn/Create?poId=5
        public async Task<IActionResult> Create(int? poId)
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

            ViewBag.WarehouseId = warehouseId;
            ViewBag.UserId = userId;

            if (poId.HasValue)
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Supplier)
                    .Include(po => po.PurchaseOrderDetails)
                        .ThenInclude(pod => pod.Product)
                            .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(po => po.PurchaseOrderId == poId.Value
                        && po.WarehouseId == warehouseId);

                if (purchaseOrder == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn mua hàng.";
                    return RedirectToAction(nameof(Index));
                }

                // Chỉ cho nhập kho nếu Delivered hoặc PartiallyReceived
                if (purchaseOrder.Status != "Delivered" && purchaseOrder.Status != "PartiallyReceived")
                {
                    TempData["Error"] = "Chỉ có thể nhập kho cho đơn hàng đã về kho.";
                    return RedirectToAction(nameof(Index));
                }

                var receivedQuantities = await GetReceivedQuantities(poId.Value);

                var model = new StockInCreateViewModel
                {
                    PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                    SupplierId = purchaseOrder.SupplierId,
                    SupplierName = purchaseOrder.Supplier.SupplierName,
                    PurchaseOrderCode = "PO-" + purchaseOrder.PurchaseOrderId.ToString("D4"),
                    ExpectedReceivingDate = purchaseOrder.ExpectedReceivingDate,
                    Details = purchaseOrder.PurchaseOrderDetails.Select(pod => new StockInDetailViewModel
                    {
                        ProductId = pod.ProductId,
                        ProductName = pod.Product.ProductName,
                        CategoryName = pod.Product.Category?.CategoryName ?? "N/A",
                        OrderedQuantity = pod.Quantity,
                        ReceivedQuantity = receivedQuantities.ContainsKey(pod.ProductId)
                            ? receivedQuantities[pod.ProductId]
                            : 0,
                        RemainingQuantity = pod.Quantity - (receivedQuantities.ContainsKey(pod.ProductId)
                            ? receivedQuantities[pod.ProductId]
                            : 0),
                        UnitPrice = pod.UnitPrice,
                        CurrentReceiving = 0,
                        LocationId = 0
                    }).ToList()
                };

                ViewBag.Locations = new SelectList(
                    await _context.Locations
                        .Where(l => l.WarehouseId == warehouseId)
                        .OrderBy(l => l.LocationCode)
                        .ToListAsync(),
                    "LocationId",
                    "LocationCode"
                );

                return View(model);
            }

            TempData["Error"] = "Vui lòng chọn đơn mua hàng để nhập kho.";
            return RedirectToAction(nameof(Index));
        }

        // POST: StockIn/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockInCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            var warehouseId = await _context.UserWarehouses
                .Where(uw => uw.UserId == userId)
                .Select(uw => uw.WarehouseId)
                .FirstOrDefaultAsync();

            if (!ModelState.IsValid || model.Details == null || !model.Details.Any(d => d.CurrentReceiving > 0))
            {
                TempData["Error"] = "Vui lòng nhập số lượng nhận hàng hợp lệ.";

                ViewBag.Locations = new SelectList(
                    await _context.Locations.Where(l => l.WarehouseId == warehouseId).ToListAsync(),
                    "LocationId",
                    "LocationCode"
                );

                return View(model);
            }

            try
            {
                var stockInReceipt = new StockInReceipt
                {
                    WarehouseId = warehouseId,
                    ReceivedBy = userId,
                    ReceivedDate = DateTime.Now,
                    Reason = "Purchase",
                    PurchaseOrderId = model.PurchaseOrderId,
                    CreatedAt = DateTime.Now
                };

                _context.StockInReceipts.Add(stockInReceipt);
                await _context.SaveChangesAsync();

                decimal totalAmount = 0;

                foreach (var detail in model.Details.Where(d => d.CurrentReceiving > 0))
                {
                    if (detail.CurrentReceiving > detail.RemainingQuantity)
                    {
                        throw new Exception($"Số lượng nhập ({detail.CurrentReceiving}) vượt quá số còn lại ({detail.RemainingQuantity}) của sản phẩm {detail.ProductName}");
                    }

                    var stockInDetail = new StockInDetail
                    {
                        StockInId = stockInReceipt.StockInId,
                        ProductId = detail.ProductId,
                        LocationId = detail.LocationId,
                        Quantity = detail.CurrentReceiving,
                        UnitPrice = detail.UnitPrice
                    };

                    _context.StockInDetails.Add(stockInDetail);
                    totalAmount += detail.CurrentReceiving * detail.UnitPrice;

                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == detail.ProductId
                            && i.LocationId == detail.LocationId);

                    if (inventory != null)
                    {
                        inventory.Quantity = (inventory.Quantity ?? 0) + detail.CurrentReceiving;
                        inventory.LastUpdated = DateTime.Now;
                    }
                    else
                    {
                        inventory = new Inventory
                        {
                            ProductId = detail.ProductId,
                            LocationId = detail.LocationId,
                            Quantity = detail.CurrentReceiving,
                            LastUpdated = DateTime.Now
                        };
                        _context.Inventories.Add(inventory);
                    }
                }

                stockInReceipt.TotalAmount = totalAmount;

                // Update PO status
                var receivedQuantities = await GetReceivedQuantities(model.PurchaseOrderId);
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.PurchaseOrderDetails)
                    .FirstOrDefaultAsync(po => po.PurchaseOrderId == model.PurchaseOrderId);

                if (purchaseOrder != null)
                {
                    bool fullyReceived = true;
                    foreach (var pod in purchaseOrder.PurchaseOrderDetails)
                    {
                        var totalReceived = receivedQuantities.ContainsKey(pod.ProductId)
                            ? receivedQuantities[pod.ProductId]
                            : 0;

                        var currentReceiving = model.Details
                            .Where(d => d.ProductId == pod.ProductId)
                            .Sum(d => d.CurrentReceiving);

                        totalReceived += currentReceiving;

                        if (totalReceived < pod.Quantity)
                        {
                            fullyReceived = false;
                            break;
                        }
                    }

                    purchaseOrder.Status = fullyReceived ? "Received" : "PartiallyReceived";
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Nhập kho thành công! Mã phiếu: SI-{stockInReceipt.StockInId:D4}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";

                ViewBag.Locations = new SelectList(
                    await _context.Locations.Where(l => l.WarehouseId == warehouseId).ToListAsync(),
                    "LocationId",
                    "LocationCode"
                );

                return View(model);
            }
        }

        // API: Get Available Locations
        [HttpGet]
        public async Task<IActionResult> GetAvailableLocations(int warehouseId, int productId)
        {
            try
            {
                var userWarehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == GetCurrentUserId())
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                if (userWarehouseId != warehouseId)
                {
                    return Json(new { error = "Không có quyền truy cập" });
                }

                var locations = await _context.Locations
                    .Where(l => l.WarehouseId == warehouseId)
                    .Select(l => new
                    {
                        locationId = l.LocationId,
                        locationCode = l.LocationCode,
                        locationName = l.LocationName,
                        rack = l.Rack ?? "N/A",
                        maxCapacity = l.Capacity,
                        currentStock = _context.Inventories
                            .Where(i => i.LocationId == l.LocationId)
                            .Sum(i => i.Quantity) ?? 0
                    })
                    .ToListAsync();

                return Json(locations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Check Location Capacity
        [HttpGet]
        public async Task<IActionResult> CheckLocationCapacity(int locationId)
        {
            try
            {
                var location = await _context.Locations
                    .FirstOrDefaultAsync(l => l.LocationId == locationId);

                if (location == null)
                {
                    return Json(new { error = "Không tìm thấy vị trí" });
                }

                var currentStock = await _context.Inventories
                    .Where(i => i.LocationId == locationId)
                    .SumAsync(i => i.Quantity) ?? 0;

                return Json(new
                {
                    locationId = location.LocationId,
                    locationCode = location.LocationCode,
                    maxCapacity = location.Capacity,
                    currentStock = currentStock
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Confirm Stock In
        [HttpPost]
        public async Task<IActionResult> ConfirmStockIn([FromBody] ConfirmStockInRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var warehouseId = await _context.UserWarehouses
                    .Where(uw => uw.UserId == userId)
                    .Select(uw => uw.WarehouseId)
                    .FirstOrDefaultAsync();

                if (request.WarehouseId != warehouseId)
                {
                    return Json(new { success = false, error = "Không có quyền truy cập" });
                }

                // Create StockInReceipt
                var stockInReceipt = new StockInReceipt
                {
                    WarehouseId = warehouseId,
                    ReceivedBy = userId,
                    ReceivedDate = DateTime.Now,
                    Reason = "Purchase",
                    PurchaseOrderId = request.PurchaseOrderId,
                    CreatedAt = DateTime.Now,
                    TotalAmount = 0
                };

                _context.StockInReceipts.Add(stockInReceipt);
                await _context.SaveChangesAsync();

                decimal totalAmount = 0;

                // Create StockInDetails and update Inventory
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
                    totalAmount += item.Quantity * item.UnitPrice;

                    // Update Inventory
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.LocationId == item.LocationId);

                    if (inventory != null)
                    {
                        inventory.Quantity = (inventory.Quantity ?? 0) + item.Quantity;
                        inventory.LastUpdated = DateTime.Now;
                    }
                    else
                    {
                        inventory = new Inventory
                        {
                            ProductId = item.ProductId,
                            LocationId = item.LocationId,
                            Quantity = item.Quantity,
                            LastUpdated = DateTime.Now
                        };
                        _context.Inventories.Add(inventory);
                    }
                }

                stockInReceipt.TotalAmount = totalAmount;

                // Update Purchase Order Status
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.PurchaseOrderDetails)
                    .FirstOrDefaultAsync(po => po.PurchaseOrderId == request.PurchaseOrderId);

                if (purchaseOrder != null)
                {
                    var receivedQuantities = await GetReceivedQuantities(request.PurchaseOrderId);
                    bool fullyReceived = true;

                    foreach (var pod in purchaseOrder.PurchaseOrderDetails)
                    {
                        var totalReceived = receivedQuantities.ContainsKey(pod.ProductId)
                            ? receivedQuantities[pod.ProductId]
                            : 0;

                        var currentReceiving = request.Items
                            .Where(i => i.ProductId == pod.ProductId)
                            .Sum(i => i.Quantity);

                        totalReceived += currentReceiving;

                        if (totalReceived < pod.Quantity)
                        {
                            fullyReceived = false;
                            break;
                        }
                    }

                    purchaseOrder.Status = fullyReceived ? "Received" : "PartiallyReceived";
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Nhập kho thành công! Mã phiếu: SI-{stockInReceipt.StockInId:D4}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private async Task<Dictionary<int, int>> GetReceivedQuantities(int purchaseOrderId)
        {
            var receivedQuantities = await _context.StockInReceipts
                .Where(si => si.PurchaseOrderId == purchaseOrderId)
                .SelectMany(si => si.StockInDetails)
                .GroupBy(sid => sid.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalReceived = g.Sum(sid => sid.Quantity)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.TotalReceived);

            return receivedQuantities;
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

        private int GetCurrentUserId()
        {
            return 4;
        }
    }

    // ViewModels
    public class StockInCreateViewModel
    {
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string PurchaseOrderCode { get; set; }
        public DateTime? ExpectedReceivingDate { get; set; }
        public List<StockInDetailViewModel> Details { get; set; } = new List<StockInDetailViewModel>();
    }

    public class StockInDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public int OrderedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public int CurrentReceiving { get; set; }
        public decimal UnitPrice { get; set; }
        public int LocationId { get; set; }
    }

    public class ConfirmStockInRequest
    {
        public int PurchaseOrderId { get; set; }
        public int WarehouseId { get; set; }
        public List<ConfirmStockInItem> Items { get; set; }
    }

    public class ConfirmStockInItem
    {
        public int ProductId { get; set; }
        public int LocationId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}