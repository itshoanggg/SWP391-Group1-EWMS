using System;
using System.Collections.Generic;

namespace EWMS.ViewModels
{
    public class NXTReportRowViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int BeginQty { get; set; }
        public int InQty { get; set; }
        public decimal InValue { get; set; }
        public int OutQty { get; set; }
        public decimal OutValue { get; set; }
        public int EndQty { get; set; }
        public decimal EndValue { get; set; }
        public decimal CostPrice { get; set; }
    }

    public class NXTReportTotals
    {
        public int BeginQty { get; set; }
        public int InQty { get; set; }
        public decimal InValue { get; set; }
        public int OutQty { get; set; }
        public decimal OutValue { get; set; }
        public int EndQty { get; set; }
        public decimal EndValue { get; set; }
    }

    public class NXTReportViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<NXTReportRowViewModel> Rows { get; set; } = new();
        public NXTReportTotals Totals { get; set; } = new();

        public int DeltaQty => (Totals.EndQty - Totals.BeginQty);
    }
}
