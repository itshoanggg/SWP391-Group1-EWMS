using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class CreateSalesOrderViewModel
    {
        [Required(ErrorMessage = "Tên khách hàng là bắt buộc")]
        [StringLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? CustomerPhone { get; set; }

        [StringLength(255)]
        public string? CustomerAddress { get; set; }

        [Required(ErrorMessage = "Ngày xuất hàng dự kiến là bắt buộc")]
        [DataType(DataType.Date)]
        public DateTime ExpectedDeliveryDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Vui lòng thêm ít nhất một sản phẩm")]
        [MinLength(1, ErrorMessage = "Vui lòng thêm ít nhất một sản phẩm")]
        public List<CreateSalesOrderDetailViewModel> Details { get; set; } = new();
    }
}
