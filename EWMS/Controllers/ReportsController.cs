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

        public ReportsController(IInventoryReportService reportService, IUserService userService)
        {
            _reportService = reportService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> NXT(int? warehouseId = null, int? year = null, int? month = null, bool print = false)
        {
            var userId = _userService.GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var allowedWarehouses = await _userService.GetWarehousesForUserAsync(userId);
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

            var range = GetDateRange(year, month);
            var vm = await _reportService.GetNXTReportAsync(selectedWarehouseId, range.from, range.to);

            ViewBag.AllowedWarehouses = allowedWarehouses;
            ViewBag.SelectedWarehouseId = selectedWarehouseId;
            ViewBag.SelectedYear = year ?? (range.to?.Year ?? DateTime.Now.Year);
            ViewBag.SelectedMonth = month;
            ViewBag.PrintMode = print;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int warehouseId, int? year = null, int? month = null)
        {
            var userId = _userService.GetCurrentUserId();
            var allowed = await _userService.GetWarehouseIdsForUserAsync(userId);
            if (!allowed.Contains(warehouseId)) return Forbid();

            var range = GetDateRange(year, month);
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
        public IActionResult ExportPdf(int warehouseId, int? year = null, int? month = null)
        {
            return RedirectToAction(nameof(NXT), new { warehouseId, year, month, print = true });
        }

        /// <summary>
        /// Calculate date range based on year and month parameters
        /// </summary>
        private static (DateTime? from, DateTime? to) GetDateRange(int? year, int? month)
        {
            if (!year.HasValue)
            {
                // Default to current month if no year specified
                var now = DateTime.Now;
                var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0);
                return (first, now);
            }

            if (month.HasValue && month.Value >= 1 && month.Value <= 12)
            {
                // Specific month and year
                var fromDt = new DateTime(year.Value, month.Value, 1, 0, 0, 0);
                var toDt = fromDt.AddMonths(1).AddTicks(-1);
                return (fromDt, toDt);
            }

            // Entire year
            var fromYear = new DateTime(year.Value, 1, 1, 0, 0, 0);
            var toYear = new DateTime(year.Value, 12, 31, 23, 59, 59);
            return (fromYear, toYear);
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
