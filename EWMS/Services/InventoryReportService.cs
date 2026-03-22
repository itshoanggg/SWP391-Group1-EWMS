using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EWMS.Models;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class InventoryReportService : IInventoryReportService
    {
        private readonly IUnitOfWork _uow;

        public InventoryReportService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<NXTReportViewModel> GetNXTReportAsync(int warehouseId, DateTime? from, DateTime? to)
        {
            var ctx = _uow.Inventories.Context;

            var effectiveTo = to ?? DateTime.Now;

            // Current ending quantities per product (as of now)
            var currentEndingQuery = ctx.Inventories
                .Include(i => i.Product)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId)
                .GroupBy(i => new { i.ProductId, i.Product.ProductName, i.Product.Unit, i.Product.CostPrice })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    Unit = g.Key.Unit ?? string.Empty,
                    CostPrice = g.Key.CostPrice ?? 0m,
                    EndQtyNow = g.Sum(x => x.Quantity ?? 0)
                });

            // Movements within the selected period [from, to]
            var inWithinQuery = ctx.StockInDetails
                .Include(d => d.StockIn)
                .Where(d => d.StockIn.WarehouseId == warehouseId
                            && (!from.HasValue || ((d.StockIn.ReceivedDate ?? DateTime.MinValue) >= from.Value))
                            && (((d.StockIn.ReceivedDate ?? DateTime.MaxValue) <= effectiveTo)))
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    InQty = g.Sum(x => x.Quantity),
                    InValue = g.Sum(x => (x.TotalPrice.HasValue ? x.TotalPrice.Value : x.UnitPrice * x.Quantity))
                });

            var outWithinQuery = ctx.StockOutDetails
                .Include(d => d.StockOut)
                .Where(d => d.StockOut.WarehouseId == warehouseId
                            && (!from.HasValue || ((d.StockOut.IssuedDate ?? DateTime.MinValue) >= from.Value))
                            && (((d.StockOut.IssuedDate ?? DateTime.MaxValue) <= effectiveTo)))
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    OutQty = g.Sum(x => x.Quantity),
                    OutValue = g.Sum(x => (x.TotalPrice.HasValue ? x.TotalPrice.Value : x.UnitPrice * x.Quantity))
                });

            // Movements AFTER the 'to' date (to recompute ending as of 'to')
            var inAfterToQuery = ctx.StockInDetails
                .Include(d => d.StockIn)
                .Where(d => d.StockIn.WarehouseId == warehouseId
                            && (((d.StockIn.ReceivedDate ?? DateTime.MinValue) > effectiveTo)))
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    InQtyAfter = g.Sum(x => x.Quantity)
                });

            var outAfterToQuery = ctx.StockOutDetails
                .Include(d => d.StockOut)
                .Where(d => d.StockOut.WarehouseId == warehouseId
                            && (((d.StockOut.IssuedDate ?? DateTime.MinValue) > effectiveTo)))
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    OutQtyAfter = g.Sum(x => x.Quantity)
                });

            var currentEnding = await currentEndingQuery.ToListAsync();
            var insWithin = await inWithinQuery.ToListAsync();
            var outsWithin = await outWithinQuery.ToListAsync();
            var insAfter = await inAfterToQuery.ToListAsync();
            var outsAfter = await outAfterToQuery.ToListAsync();

            var productIds = currentEnding.Select(e => e.ProductId)
                .Union(insWithin.Select(i => i.ProductId))
                .Union(outsWithin.Select(o => o.ProductId))
                .Union(insAfter.Select(i => i.ProductId))
                .Union(outsAfter.Select(o => o.ProductId))
                .ToHashSet();

            var rows = new List<NXTReportRowViewModel>();

            foreach (var pid in productIds)
            {
                var e = currentEnding.FirstOrDefault(x => x.ProductId == pid);
                var iWithin = insWithin.FirstOrDefault(x => x.ProductId == pid);
                var oWithin = outsWithin.FirstOrDefault(x => x.ProductId == pid);
                var iAfter = insAfter.FirstOrDefault(x => x.ProductId == pid);
                var oAfter = outsAfter.FirstOrDefault(x => x.ProductId == pid);

                var endNow = e?.EndQtyNow ?? 0;
                var inAfterQty = iAfter?.InQtyAfter ?? 0;
                var outAfterQty = oAfter?.OutQtyAfter ?? 0;

                // Ending qty as of 'effectiveTo'
                var endAtTo = endNow - inAfterQty + outAfterQty;

                var inQty = iWithin?.InQty ?? 0;
                var outQty = oWithin?.OutQty ?? 0;
                var beginQty = endAtTo - inQty + outQty;

                var cost = e?.CostPrice ?? 0m;
                var pname = e?.ProductName ?? $"Product #{pid}";
                var unit = e?.Unit ?? string.Empty;

                rows.Add(new NXTReportRowViewModel
                {
                    ProductId = pid,
                    ProductName = pname,
                    Unit = unit,
                    BeginQty = beginQty,
                    InQty = inQty,
                    InValue = iWithin?.InValue ?? 0m,
                    OutQty = outQty,
                    OutValue = oWithin?.OutValue ?? 0m,
                    EndQty = endAtTo,
                    EndValue = endAtTo * cost,
                    CostPrice = cost
                });
            }

            rows = rows.OrderBy(r => r.ProductName).ToList();

            var totals = new NXTReportTotals
            {
                BeginQty = rows.Sum(r => r.BeginQty),
                InQty = rows.Sum(r => r.InQty),
                InValue = rows.Sum(r => r.InValue),
                OutQty = rows.Sum(r => r.OutQty),
                OutValue = rows.Sum(r => r.OutValue),
                EndQty = rows.Sum(r => r.EndQty),
                EndValue = rows.Sum(r => r.EndValue)
            };

            var warehouseName = await _uow.Warehouses.GetWarehouseNameByIdAsync(warehouseId) ?? $"Warehouse #{warehouseId}";

            return new NXTReportViewModel
            {
                WarehouseId = warehouseId,
                WarehouseName = warehouseName,
                FromDate = from,
                ToDate = effectiveTo,
                Rows = rows,
                Totals = totals
            };
        }
    }
}
