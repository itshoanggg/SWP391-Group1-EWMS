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
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
        public string Username { get; set; } = string.Empty;

        [StringLength(255)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string? Password { get; set; }

        [StringLength(100)]
        [RegularExpression(@"^[\p{L}\sa-zA-Z0-9]+$", ErrorMessage = "Full name can only contain letters, numbers, and spaces")]
        public string? FullName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [RegularExpression(@"^[0-9+\-\s]{10,20}$", ErrorMessage = "Phone number must be 10-20 digits")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Role is required")]
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
