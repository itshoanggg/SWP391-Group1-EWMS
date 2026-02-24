using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class CreateStockOutDetailViewModel
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "The location must be selected.")]
        public int LocationId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "The number must be greater than 0.")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "The unit price must be greater than 0.")]
        public decimal UnitPrice { get; set; }
    }
}
