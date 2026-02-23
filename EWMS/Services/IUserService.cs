using EWMS.Models;

public interface IUserService
{
    Task<User?> ValidateUserAsync(string username, string password);
}