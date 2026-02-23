using EWMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    // Bạn phải thêm ": IUserService" ở đây để báo cho hệ thống 
    // rằng UserService thực thi các hàm của IUserService.
    public class UserService : IUserService
    {
        private readonly EWMSDbContext _context;

        public UserService(EWMSDbContext context)
        {
            _context = context;
        }

        public async Task<User?> ValidateUserAsync(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.UserWarehouses)
                .FirstOrDefaultAsync(u =>
                    u.Email == email && u.IsActive == true);

            if (user == null)
                return null;

            if (user.PasswordHash != password)
                return null;

            return user;
        }
    }
}