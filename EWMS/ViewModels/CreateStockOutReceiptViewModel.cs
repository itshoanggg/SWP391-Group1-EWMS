using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class CreateStockOutReceiptViewModel
    {
        [Required(ErrorMessage = "Please select order.")]
        public int SalesOrderId { get; set; }

        [Required(ErrorMessage = "Please select the release date.")]
        public DateTime IssuedDate { get; set; }

        public int WarehouseId { get; set; }

        [StringLength(30)]
        public string? Reason { get; set; } = "Sale";

        [Required(ErrorMessage = "Please select a location for the products.")]
        public List<CreateStockOutDetailViewModel> Details { get; set; } = new List<CreateStockOutDetailViewModel>();
    }
}
