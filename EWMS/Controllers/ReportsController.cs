using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Warehouse Manager")]
    public class ReportsController : Controller
    {
        private readonly IInventoryReportService _reportService;
        private readonly IUserService _userService;
        private readonly IUnitOfWork _uow;

        public ReportsController(IInventoryReportService reportService, IUserService userService, IUnitOfWork uow)
        {
            _reportService = reportService;
            _userService = userService;
            _uow = uow;
        }

        [HttpGet]
        public async Task<IActionResult> NXT(int? warehouseId = null, string? period = "month", int? year = null, int? month = null, bool print = false)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var allowedWarehouses = await _uow.UserWarehouses.GetWarehousesForUserAsync(userId);
            if (allowedWarehouses.Count == 0)
            {
                TempData["Error"] = "You are not assigned to any warehouse.";
                return RedirectToAction("Index", "Home");
            }

            int selectedWarehouseId = warehouseId ?? allowedWarehouses.First().WarehouseId;
            if (!allowedWarehouses.Any(w => w.WarehouseId == selectedWarehouseId))
            {
                return Forbid();
            }

            (DateTime? from, DateTime? to) range;
            if (year.HasValue)
            {
                if (month.HasValue && month.Value >= 1 && month.Value <= 12)
                {
                    var fromDt = new DateTime(year.Value, month.Value, 1, 0, 0, 0);
                    var toDt = fromDt.AddMonths(1).AddTicks(-1); // end of selected month
                    range = (fromDt, toDt);
                }
                else
                {
                    var fromDt = new DateTime(year.Value, 1, 1, 0, 0, 0);
                    var toDt = new DateTime(year.Value, 12, 31, 23, 59, 59);
                    range = (fromDt, toDt);
                }
            }
            else
            {
                range = ResolvePeriod(period ?? "month");
            }

            var vm = await _reportService.GetNXTReportAsync(selectedWarehouseId, range.from, range.to);

            ViewBag.Period = period ?? "month";
            ViewBag.AllowedWarehouses = allowedWarehouses;
            ViewBag.SelectedWarehouseId = selectedWarehouseId;
            ViewBag.SelectedYear = year ?? (range.to?.Year ?? DateTime.Now.Year);
            ViewBag.SelectedMonth = month ?? ((period == "month") ? (int?)(range.to?.Month ?? DateTime.Now.Month) : null);
            ViewBag.PrintMode = print;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int warehouseId, string? period = "month", int? year = null, int? month = null)
        {
            var userId = _userService.GetCurrentUserId();
            var allowed = await _uow.UserWarehouses.GetWarehouseIdsForUserAsync(userId);
            if (!allowed.Contains(warehouseId)) return Forbid();

            (DateTime? from, DateTime? to) range;
            if (year.HasValue)
            {
                if (month.HasValue && month.Value >= 1 && month.Value <= 12)
                {
                    var fromDt = new DateTime(year.Value, month.Value, 1, 0, 0, 0);
                    var toDt = fromDt.AddMonths(1).AddTicks(-1);
                    range = (fromDt, toDt);
                }
                else
                {
                    var fromDt = new DateTime(year.Value, 1, 1, 0, 0, 0);
                    var toDt = new DateTime(year.Value, 12, 31, 23, 59, 59);
                    range = (fromDt, toDt);
                }
            }
            else
            {
                range = ResolvePeriod(period ?? "month");
            }

            var vm = await _reportService.GetNXTReportAsync(warehouseId, range.from, range.to);

            var lines = new List<string>
            {
                "Product,Unit,Begin Qty,In Qty,Out Qty,End Qty,End Value"
            };
            foreach (var r in vm.Rows)
            {
                lines.Add($"{Escape(r.ProductName)},{Escape(r.Unit)},{r.BeginQty},{r.InQty},{r.OutQty},{r.EndQty},{r.EndValue}");
            }
            lines.Add($"TOTAL,,{vm.Totals.BeginQty},{vm.Totals.InQty},{vm.Totals.OutQty},{vm.Totals.EndQty},{vm.Totals.EndValue}");

            var csv = string.Join("\r\n", lines);
            var bytes = Encoding.UTF8.GetBytes(csv);
            var safeName = (vm.WarehouseName ?? "Warehouse").Replace(' ', '_');
            var fileName = $"NXT_Report_{safeName}_{DateTime.Now:yyyyMMddHHmm}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpGet]
        public IActionResult ExportPdf(int warehouseId, string? period = "month", int? year = null, int? month = null)
        {
            return RedirectToAction(nameof(NXT), new { warehouseId, period, year, month, print = true });
        }

        private static (DateTime? from, DateTime? to) ResolvePeriod(string period)
        {
            var now = DateTime.Now;
            switch (period.ToLowerInvariant())
            {
                case "month":
                    var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0);
                    return (first, now);
                case "quarter":
                    int q = (now.Month - 1) / 3 + 1;
                    int startMonth = (q - 1) * 3 + 1;
                    var qfirst = new DateTime(now.Year, startMonth, 1, 0, 0, 0);
                    return (qfirst, now);
                case "year":
                    var yfirst = new DateTime(now.Year, 1, 1, 0, 0, 0);
                    return (yfirst, now);
                default:
                    return (null, null);
            }
        }

        private static string Escape(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Contains("\"") || input.Contains(","))
            {
                return "\"" + input.Replace("\"", "\"\"") + "\"";
            }
            return input;
        }
    }
}
