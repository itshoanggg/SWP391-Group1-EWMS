using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class CreateSalesOrderViewModel
    {
        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(150)]
        [RegularExpression(@"^[a-zA-ZÀ-ỹ\s]+$", ErrorMessage = "Customer name can only contain letters and spaces")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression(@"^[0-9+\-\s()]*$", ErrorMessage = "Phone number can only contain numbers and characters: + - ( ) space")]
        public string? CustomerPhone { get; set; }

        [StringLength(255)]
        public string? CustomerAddress { get; set; }

        [Required(ErrorMessage = "Estimated shipping date is required")]
        [DataType(DataType.Date)]
        public DateTime ExpectedDeliveryDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Please add at least one product")]
        [MinLength(1, ErrorMessage = "Please add at least one product")]
        public List<CreateSalesOrderDetailViewModel> Details { get; set; } = new();
    }
}
