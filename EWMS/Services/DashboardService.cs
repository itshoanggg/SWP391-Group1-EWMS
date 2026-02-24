using EWMS.DTOs;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DashboardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<KPIMetricsDTO> GetKPIMetricsAsync(int warehouseId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            toDate ??= DateTime.Today;

            // Revenue
            var revenue = await _unitOfWork.Inventories.Context.SalesOrders
                .Where(so => so.WarehouseId == warehouseId
                    && so.Status == "Completed"
                    && so.CreatedAt >= fromDate
                    && so.CreatedAt <= toDate)
                .SumAsync(so => so.TotalAmount);

            // Profit
            var profitData = await (from so in _unitOfWork.Inventories.Context.SalesOrders
                                    join sod in _unitOfWork.Inventories.Context.SalesOrderDetails on so.SalesOrderId equals sod.SalesOrderId
                                    join p in _unitOfWork.Inventories.Context.Products on sod.ProductId equals p.ProductId
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

            // Today orders
            var todayOrders = await _unitOfWork.Inventories.Context.SalesOrders
                .Where(so => so.WarehouseId == warehouseId
                    && so.CreatedAt.Date == DateTime.Today)
                .CountAsync();

            // Inventory value
            var inventoryValue = await (from inv in _unitOfWork.Inventories.Context.Inventories
                                        join loc in _unitOfWork.Inventories.Context.Locations on inv.LocationId equals loc.LocationId
                                        join p in _unitOfWork.Inventories.Context.Products on inv.ProductId equals p.ProductId
                                        where loc.WarehouseId == warehouseId
                                        select (inv.Quantity ?? 0) * (p.CostPrice ?? 0))
                                       .SumAsync();

            return new KPIMetricsDTO
            {
                Revenue = revenue,
                Profit = totalProfit,
                TodayOrders = todayOrders,
                InventoryValue = inventoryValue
            };
        }

        public async Task<IEnumerable<StockMovementDTO>> GetStockMovementAsync(int warehouseId, string period = "week")
        {
            DateTime fromDate = period.ToLower() switch
            {
                "week" => DateTime.Today.AddDays(-7),
                "month" => DateTime.Today.AddMonths(-1),
                "year" => DateTime.Today.AddYears(-1),
                _ => DateTime.Today.AddDays(-7)
            };

            var stockInData = await (from si in _unitOfWork.Inventories.Context.StockInReceipts
                                     join sid in _unitOfWork.Inventories.Context.StockInDetails on si.StockInId equals sid.StockInId
                                     where si.WarehouseId == warehouseId && si.ReceivedDate >= fromDate
                                     group sid by si.ReceivedDate!.Value.Date into g
                                     select new
                                     {
                                         Date = g.Key,
                                         Quantity = g.Sum(x => x.Quantity)
                                     }).OrderBy(x => x.Date).ToListAsync();

            var stockOutData = await (from so in _unitOfWork.Inventories.Context.StockOutReceipts
                                      join sod in _unitOfWork.Inventories.Context.StockOutDetails on so.StockOutId equals sod.StockOutId
                                      where so.WarehouseId == warehouseId && so.IssuedDate >= fromDate
                                      group sod by so.IssuedDate!.Value.Date into g
                                      select new
                                      {
                                          Date = g.Key,
                                          Quantity = g.Sum(x => x.Quantity)
                                      }).OrderBy(x => x.Date).ToListAsync();

            var currentStock = await (from inv in _unitOfWork.Inventories.Context.Inventories
                                      join loc in _unitOfWork.Inventories.Context.Locations on inv.LocationId equals loc.LocationId
                                      where loc.WarehouseId == warehouseId
                                      select inv.Quantity ?? 0).SumAsync();

            var dates = new List<DateTime>();
            for (var date = fromDate.Date; date <= DateTime.Today; date = date.AddDays(1))
            {
                dates.Add(date);
            }

            return dates.Select(d => new StockMovementDTO
            {
                Date = d.ToString("dd/MM"),
                StockIn = stockInData.FirstOrDefault(x => x.Date == d)?.Quantity ?? 0,
                StockOut = stockOutData.FirstOrDefault(x => x.Date == d)?.Quantity ?? 0,
                Stock = currentStock
            }).ToList();
        }

        public async Task<IEnumerable<SalesRevenueDTO>> GetSalesRevenueAsync(int warehouseId, string period = "month")
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

            var salesData = await _unitOfWork.Inventories.Context.SalesOrders
                .Where(so => so.WarehouseId == warehouseId
                    && so.CreatedAt >= fromDate
                    && so.Status == "Completed"
                    )
                .GroupBy(so => so.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Quantity = g.Count(),
                    Revenue = g.Sum(so => so.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return salesData.Select(s => new SalesRevenueDTO
            {
                Date = s.Date.ToString(groupFormat),
                Quantity = s.Quantity,
                Revenue = s.Revenue
            }).ToList();
        }

        public async Task<IEnumerable<CategoryDistributionDTO>> GetCategoryDistributionAsync(int warehouseId)
        {
            var categoryData = await (from inv in _unitOfWork.Inventories.Context.Inventories
                                      join loc in _unitOfWork.Inventories.Context.Locations on inv.LocationId equals loc.LocationId
                                      join p in _unitOfWork.Inventories.Context.Products on inv.ProductId equals p.ProductId
                                      join pc in _unitOfWork.Inventories.Context.ProductCategories on p.CategoryId equals pc.CategoryId
                                      where loc.WarehouseId == warehouseId
                                      group inv by pc.CategoryName into g
                                      select new CategoryDistributionDTO
                                      {
                                          Category = g.Key,
                                          Quantity = g.Sum(x => x.Quantity ?? 0)
                                      })
                                     .OrderByDescending(x => x.Quantity)
                                     .ToListAsync();

            return categoryData;
        }

        public async Task<IEnumerable<RecentActivityDTO>> GetRecentActivitiesAsync(int warehouseId, int limit = 10)
        {
            var stockIns = await (from si in _unitOfWork.Inventories.Context.StockInReceipts
                                  join u in _unitOfWork.Inventories.Context.Users on si.ReceivedBy equals u.UserId
                                  where si.WarehouseId == warehouseId
                                  orderby si.ReceivedDate descending
                                  select new RecentActivityDTO
                                  {
                                      Type = "Nhập kho",
                                      Date = si.ReceivedDate,
                                      Description = "Nhập " + _unitOfWork.Inventories.Context.StockInDetails
                                          .Where(sid => sid.StockInId == si.StockInId)
                                          .Sum(sid => sid.Quantity) + " sản phẩm",
                                      Amount = si.TotalAmount ?? 0,
                                      User = u.FullName ?? u.Username
                                  })
                                 .Take(limit)
                                 .ToListAsync();

            var stockOuts = await (from so in _unitOfWork.Inventories.Context.StockOutReceipts
                                   join u in _unitOfWork.Inventories.Context.Users on so.IssuedBy equals u.UserId
                                   where so.WarehouseId == warehouseId
                                   orderby so.IssuedDate descending
                                   select new RecentActivityDTO
                                   {
                                       Type = "Xuất kho",
                                       Date = so.IssuedDate,
                                       Description = "Xuất " + _unitOfWork.Inventories.Context.StockOutDetails
                                           .Where(sod => sod.StockOutId == so.StockOutId)
                                           .Sum(sod => sod.Quantity) + " sản phẩm",
                                       Amount = so.TotalAmount ?? 0,
                                       User = u.FullName ?? u.Username
                                   })
                                  .Take(limit)
                                  .ToListAsync();

            return stockIns.Concat(stockOuts)
                .OrderByDescending(a => a.Date)
                .Take(limit)
                .ToList();
        }

        public async Task<IEnumerable<LowStockAlertDTO>> GetLowStockAlertsAsync(int warehouseId, int threshold = 10)
        {
            var lowStockProducts = await (from inv in _unitOfWork.Inventories.Context.Inventories
                                          join loc in _unitOfWork.Inventories.Context.Locations on inv.LocationId equals loc.LocationId
                                          join p in _unitOfWork.Inventories.Context.Products on inv.ProductId equals p.ProductId
                                          where loc.WarehouseId == warehouseId && inv.Quantity <= threshold
                                          select new LowStockAlertDTO
                                          {
                                              ProductId = p.ProductId,
                                              ProductName = p.ProductName,
                                              Quantity = inv.Quantity ?? 0,
                                              LocationCode = loc.LocationCode
                                          })
                                         .OrderBy(x => x.Quantity)
                                         .ToListAsync();

            return lowStockProducts;
        }

        public async Task<InventoryStatsDTO> GetInventoryStatsAsync(int warehouseId)
        {
            var inventories = await (from inv in _unitOfWork.Inventories.Context.Inventories
                                     join loc in _unitOfWork.Inventories.Context.Locations on inv.LocationId equals loc.LocationId
                                     where loc.WarehouseId == warehouseId
                                     select inv)
                                    .ToListAsync();

            var totalProducts = inventories
                .Select(i => i.ProductId)
                .Distinct()
                .Count();

            var totalStock = inventories.Sum(i => i.Quantity ?? 0);

            var lowStockCount = inventories
                .Count(i => i.Quantity > 0 && i.Quantity < 10);

            var outOfStockCount = inventories
                .Count(i => i.Quantity == 0);

            return new InventoryStatsDTO
            {
                TotalProducts = totalProducts,
                TotalStock = totalStock,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount
            };
        }
    }
}
