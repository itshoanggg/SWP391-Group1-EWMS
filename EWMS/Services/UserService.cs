using EWMS.Models;
using Microsoft.EntityFrameworkCore;

public class UserService : IUserService
{
    private readonly EWMSDbContext _context;

    public UserService(EWMSDbContext context)
    {
        _context = context;
    }

    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.UserWarehouses)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive == true);

        if (user == null)
            return null;

        // ⚠️ TẠM SO SÁNH PLAIN TEXT (nếu bạn chưa hash)
        if (user.PasswordHash != password)
            return null;

        return user;
    }
}