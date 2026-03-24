using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class InternalTransferViewModel
    {
        [Required(ErrorMessage = "Please select source rack.")]
        public string FromRack { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select source location.")]
        public int FromLocationId { get; set; }

        [Required(ErrorMessage = "Please select destination rack.")]
        public string ToRack { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select destination location.")]
        public int ToLocationId { get; set; }

        [Required(ErrorMessage = "Please select a product.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Quantity must be greater than 0.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
        public int Quantity { get; set; }

        public string? Reason { get; set; }
    }
}
