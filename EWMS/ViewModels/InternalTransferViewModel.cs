using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class InternalTransferViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn Rack nguồn.")]
        public string FromRack { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn Vị trí nguồn.")]
        public int FromLocationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Rack đích.")]
        public string ToRack { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn Vị trí đích.")]
        public int ToLocationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Số lượng phải lớn hơn 0.")]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
        public int Quantity { get; set; }

        public string? Reason { get; set; }
    }
}
