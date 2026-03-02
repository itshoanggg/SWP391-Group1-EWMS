using System;
using System.Collections.Generic;

namespace EWMS.ViewModels
{
    public class StockHistoryViewModel
    {
        public StockOutReceiptListViewModel StockOut { get; set; } = new StockOutReceiptListViewModel();

        public List<StockInReceiptItemViewModel> StockIns { get; set; } = new List<StockInReceiptItemViewModel>();

        // Filters
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? FilterCustomer { get; set; }
        public string? FilterIssuedBy { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
    }
}
