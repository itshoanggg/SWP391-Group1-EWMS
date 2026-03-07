using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace EWMS.ViewModels
{
    public class UserViewModels : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }

    public class UserListItemViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> Warehouses { get; set; } = new();
    }

    public class CreateUserViewModel
    {
        [Required]
        [StringLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa các ký tự chữ cái, số và dấu gạch dưới, không bao gồm khoảng trắng hoặc ký tự đặc biệt.")]
        public string Username { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Password { get; set; } // plain for now; replace with hash later

        [StringLength(100)]
        public string? FullName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ. Vui lòng bắt đầu bằng số 0 và có từ 10-11 chữ số.")]
        public string? Phone { get; set; }

        [Required]
        public int RoleId { get; set; }

        public bool IsActive { get; set; } = true;

        public List<int>? WarehouseIds { get; set; }
    }

    public class EditUserViewModel : CreateUserViewModel
    {
        public int UserId { get; set; }
    }

    public class AssignRoleViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        [Required]
        public int RoleId { get; set; }
    }

    public class WarehouseSelectItem
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
    }

    public class AssignDepartmentsViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public List<int> SelectedWarehouseIds { get; set; } = new();
        public List<WarehouseSelectItem> AvailableWarehouses { get; set; } = new();
    }
}
