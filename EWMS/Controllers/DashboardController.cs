using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EWMS.Controllers
{
    public class DashboardController : Controller
    {
        private readonly EWMSContext _context;

        public DashboardController(EWMSContext context)
        {
            _context = context;
        }

        // GET: Dashboard/Index
        public async Task<IActionResult> InventoryDashboard()
        {
            // Lấy UserID từ session/authentication
            //var userId = GetCurrentUserId();
            var userId = 7;

            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy WarehouseID của user từ UserWarehouses
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

            return View("InventoryDashboard");
        }

        // API: Get KPI Metrics
        [HttpGet]
        public async Task<IActionResult> GetKPIMetrics(int warehouseId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                fromDate ??= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                toDate ??= DateTime.Today;

                // 1. Doanh thu tháng này
                var revenue = await _context.SalesOrders
                    .Where(so => so.WarehouseId == warehouseId
                        && so.Status == "Completed"
                        && so.CreatedAt >= fromDate
                        && so.CreatedAt <= toDate)
                    .SumAsync(so => so.TotalAmount ?? 0);

                // 2. Lợi nhuận tháng này
                var profitData = await (from so in _context.SalesOrders
                                        join sod in _context.SalesOrderDetails on so.SalesOrderId equals sod.SalesOrderId
                                        join p in _context.Products on sod.ProductId equals p.ProductId
                                        where so.WarehouseId == warehouseId
                                            && so.Status == "Completed"
                                            && so.CreatedAt >= fromDate
                                            && so.CreatedAt <= toDate
                                        select new
                                        {
                                            Revenue = sod.Quantity * sod.UnitPrice,
                                            Cost = sod.Quantity * (p.CostPrice ?? 0)
                                        }).ToListAsync();

                var totalProfit = profitData.Sum(x => x.Revenue - x.Cost);

                // 3. Đơn hàng hôm nay
                var todayOrders = await _context.SalesOrders
                    .Where(so => so.WarehouseId == warehouseId
                        && so.CreatedAt.HasValue
                        && so.CreatedAt.Value.Date == DateTime.Today)
                    .CountAsync();

                // 4. Giá trị tồn kho hiện tại
                var inventoryValue = await (from inv in _context.Inventories
                                            join loc in _context.Locations on inv.LocationId equals loc.LocationId
                                            join p in _context.Products on inv.ProductId equals p.ProductId
                                            where loc.WarehouseId == warehouseId
                                            select (inv.Quantity ?? 0) * (p.CostPrice ?? 0))
                                           .SumAsync();

                return Json(new
                {
                    revenue,
                    profit = totalProfit,
                    todayOrders,
                    inventoryValue
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Stock Movement Chart Data
        [HttpGet]
        public async Task<IActionResult> GetStockMovement(int warehouseId, string period = "week")
        {
            try
            {
                DateTime fromDate;
                switch (period.ToLower())
                {
                    case "week":
                        fromDate = DateTime.Today.AddDays(-7);
                        break;
                    case "month":
                        fromDate = DateTime.Today.AddMonths(-1);
                        break;
                    case "year":
                        fromDate = DateTime.Today.AddYears(-1);
                        break;
                    default:
                        fromDate = DateTime.Today.AddDays(-7);
                        break;
                }

                // Nhập kho theo ngày
                var stockInData = await (from si in _context.StockInReceipts
                                         join sid in _context.StockInDetails on si.StockInId equals sid.StockInId
                                         where si.WarehouseId == warehouseId
                                             && si.ReceivedDate >= fromDate
                                         group sid by si.ReceivedDate.Value.Date into g
                                         select new
                                         {
                                             Date = g.Key,
                                             Quantity = g.Sum(x => x.Quantity)
                                         })
                                        .OrderBy(x => x.Date)
                                        .ToListAsync();

                // Xuất kho theo ngày
                var stockOutData = await (from so in _context.StockOutReceipts
                                          join sod in _context.StockOutDetails on so.StockOutId equals sod.StockOutId
                                          where so.WarehouseId == warehouseId
                                              && so.IssuedDate >= fromDate
                                          group sod by so.IssuedDate.Value.Date into g
                                          select new
                                          {
                                              Date = g.Key,
                                              Quantity = g.Sum(x => x.Quantity)
                                          })
                                         .OrderBy(x => x.Date)
                                         .ToListAsync();

                // Tồn kho hiện tại
                var currentStock = await (from inv in _context.Inventories
                                          join loc in _context.Locations on inv.LocationId equals loc.LocationId
                                          where loc.WarehouseId == warehouseId
                                          select inv.Quantity ?? 0)
                                         .SumAsync();

                // Tạo danh sách ngày đầy đủ
                var dates = new List<DateTime>();
                for (var date = fromDate.Date; date <= DateTime.Today; date = date.AddDays(1))
                {
                    dates.Add(date);
                }

                var result = dates.Select(d => new
                {
                    date = d.ToString("dd/MM"),
                    stockIn = stockInData.FirstOrDefault(x => x.Date == d)?.Quantity ?? 0,
                    stockOut = stockOutData.FirstOrDefault(x => x.Date == d)?.Quantity ?? 0,
                    stock = currentStock
                }).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Sales Revenue Chart Data
        [HttpGet]
        public async Task<IActionResult> GetSalesRevenue(int warehouseId, string period = "month")
        {
            try
            {
                DateTime fromDate;
                string groupFormat;

                switch (period.ToLower())
                {
                    case "week":
                        fromDate = DateTime.Today.AddDays(-7);
                        groupFormat = "dd/MM";
                        break;
                    case "month":
                        fromDate = DateTime.Today.AddMonths(-1);
                        groupFormat = "dd/MM";
                        break;
                    case "year":
                        fromDate = DateTime.Today.AddYears(-1);
                        groupFormat = "MM/yyyy";
                        break;
                    default:
                        fromDate = DateTime.Today.AddMonths(-1);
                        groupFormat = "dd/MM";
                        break;
                }

                var salesData = await _context.SalesOrders
                    .Where(so => so.WarehouseId == warehouseId
                        && so.CreatedAt >= fromDate
                        && so.Status == "Completed"
                        && so.CreatedAt.HasValue)
                    .GroupBy(so => so.CreatedAt.Value.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Quantity = g.Count(),
                        Revenue = g.Sum(so => so.TotalAmount ?? 0)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                var result = salesData.Select(s => new
                {
                    date = s.Date.ToString(groupFormat),
                    quantity = s.Quantity,
                    revenue = s.Revenue
                }).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Product Category Distribution
        [HttpGet]
        public async Task<IActionResult> GetCategoryDistribution(int warehouseId)
        {
            try
            {
                var categoryData = await (from inv in _context.Inventories
                                          join loc in _context.Locations on inv.LocationId equals loc.LocationId
                                          join p in _context.Products on inv.ProductId equals p.ProductId
                                          join pc in _context.ProductCategories on p.CategoryId equals pc.CategoryId
                                          where loc.WarehouseId == warehouseId
                                          group inv by pc.CategoryName into g
                                          select new
                                          {
                                              category = g.Key,
                                              quantity = g.Sum(x => x.Quantity ?? 0)
                                          })
                                         .OrderByDescending(x => x.quantity)
                                         .ToListAsync();

                return Json(categoryData);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Recent Activities
        [HttpGet]
        public async Task<IActionResult> GetRecentActivities(int warehouseId, int limit = 10)
        {
            try
            {
                // Stock In Activities
                var stockIns = await (from si in _context.StockInReceipts
                                      join u in _context.Users on si.ReceivedBy equals u.UserId
                                      where si.WarehouseId == warehouseId
                                      orderby si.ReceivedDate descending
                                      select new
                                      {
                                          type = "Nhập kho",
                                          date = si.ReceivedDate,
                                          description = "Nhập " + _context.StockInDetails
                                              .Where(sid => sid.StockInId == si.StockInId)
                                              .Sum(sid => sid.Quantity) + " sản phẩm",
                                          amount = si.TotalAmount ?? 0,
                                          user = u.FullName ?? u.Username
                                      })
                                     .Take(limit)
                                     .ToListAsync();

                // Stock Out Activities
                var stockOuts = await (from so in _context.StockOutReceipts
                                       join u in _context.Users on so.IssuedBy equals u.UserId
                                       where so.WarehouseId == warehouseId
                                       orderby so.IssuedDate descending
                                       select new
                                       {
                                           type = "Xuất kho",
                                           date = so.IssuedDate,
                                           description = "Xuất " + _context.StockOutDetails
                                               .Where(sod => sod.StockOutId == so.StockOutId)
                                               .Sum(sod => sod.Quantity) + " sản phẩm",
                                           amount = so.TotalAmount ?? 0,
                                           user = u.FullName ?? u.Username
                                       })
                                      .Take(limit)
                                      .ToListAsync();

                var activities = stockIns.Concat(stockOuts)
                    .OrderByDescending(a => a.date)
                    .Take(limit)
                    .ToList();

                return Json(activities);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API: Get Low Stock Alerts
        [HttpGet]
        public async Task<IActionResult> GetLowStockAlerts(int warehouseId, int threshold = 10)
        {
            try
            {
                var lowStockProducts = await (from inv in _context.Inventories
                                              join loc in _context.Locations on inv.LocationId equals loc.LocationId
                                              join p in _context.Products on inv.ProductId equals p.ProductId
                                              where loc.WarehouseId == warehouseId
                                                  && inv.Quantity <= threshold
                                              select new
                                              {
                                                  productId = p.ProductId,
                                                  productName = p.ProductName,
                                                  quantity = inv.Quantity ?? 0,
                                                  locationCode = loc.LocationCode
                                              })
                                             .OrderBy(x => x.quantity)
                                             .ToListAsync();

                return Json(lowStockProducts);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Helper method - Lấy UserID hiện tại
        // TODO: Thay thế bằng authentication thực tế
        private int GetCurrentUserId()
        {
            // Option 1: Session-based
            // return HttpContext.Session.GetInt32("UserId") ?? 0;

            // Option 2: Claims-based
            // var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // return int.Parse(userIdClaim ?? "0");

            // Option 3: For testing - REMOVE in production
            return 7; // Default: hn_inventory_01
        }
    }
}