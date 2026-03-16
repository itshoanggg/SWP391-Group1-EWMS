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

            var endingQuery = ctx.Inventories
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
                    EndQty = g.Sum(x => x.Quantity ?? 0)
                });

            var inQuery = ctx.StockInDetails
                .Include(d => d.StockIn)
                .Where(d => d.StockIn.WarehouseId == warehouseId
                            && (!from.HasValue || (d.StockIn.ReceivedDate ?? DateTime.MinValue) >= from.Value)
                            && (!to.HasValue || (d.StockIn.ReceivedDate ?? DateTime.MaxValue) <= to.Value))
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    InQty = g.Sum(x => x.Quantity),
                    InValue = g.Sum(x => (x.TotalPrice.HasValue ? x.TotalPrice.Value : x.UnitPrice * x.Quantity))
                });

            var outQuery = ctx.StockOutDetails
                .Include(d => d.StockOut)
                .Where(d => d.StockOut.WarehouseId == warehouseId
                            && (!from.HasValue || (d.StockOut.IssuedDate ?? DateTime.MinValue) >= from.Value)
                            && (!to.HasValue || (d.StockOut.IssuedDate ?? DateTime.MaxValue) <= to.Value))
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    OutQty = g.Sum(x => x.Quantity),
                    OutValue = g.Sum(x => (x.TotalPrice.HasValue ? x.TotalPrice.Value : x.UnitPrice * x.Quantity))
                });

            var ending = await endingQuery.ToListAsync();
            var ins = await inQuery.ToListAsync();
            var outs = await outQuery.ToListAsync();

            var productIds = ending.Select(e => e.ProductId)
                .Union(ins.Select(i => i.ProductId))
                .Union(outs.Select(o => o.ProductId))
                .ToHashSet();

            var rows = new List<NXTReportRowViewModel>();

            foreach (var pid in productIds)
            {
                var e = ending.FirstOrDefault(x => x.ProductId == pid);
                var i = ins.FirstOrDefault(x => x.ProductId == pid);
                var o = outs.FirstOrDefault(x => x.ProductId == pid);

                var endQty = e?.EndQty ?? 0;
                var inQty = i?.InQty ?? 0;
                var outQty = o?.OutQty ?? 0;
                var beginQty = endQty - inQty + outQty;

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
                    InValue = i?.InValue ?? 0m,
                    OutQty = outQty,
                    OutValue = o?.OutValue ?? 0m,
                    EndQty = endQty,
                    EndValue = endQty * cost,
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
                ToDate = to,
                Rows = rows,
                Totals = totals
            };
        }
    }
}
