using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class CreateSalesOrderDetailViewModel
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Đơn giá phải lớn hơn 0")]
        public decimal UnitPrice { get; set; }
    }
}
