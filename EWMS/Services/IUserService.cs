using EWMS.Models;

namespace EWMS.Services
{
    public interface IUserService
    {
        Task<User?> ValidateUserAsync(string email, string password);
    }
}