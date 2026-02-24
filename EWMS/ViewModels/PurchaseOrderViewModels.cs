using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class PurchaseOrderCreateViewModel
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày nhận hàng dự kiến")]
        [Display(Name = "Ngày nhận hàng dự kiến")]
        public DateTime? ExpectedReceivingDate { get; set; }

        public List<PurchaseOrderDetailViewModel> Details { get; set; } = new List<PurchaseOrderDetailViewModel>();
    }

    public class PurchaseOrderDetailViewModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
