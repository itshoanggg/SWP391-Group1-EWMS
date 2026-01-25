using System.ComponentModel.DataAnnotations;
using System.Data;

namespace EWMS.Models
{
    public class User
    {
        public int UserId { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string FullName { get; set; }

        public int RoleId { get; set; }
        public Role Role { get; set; }
    }
}
