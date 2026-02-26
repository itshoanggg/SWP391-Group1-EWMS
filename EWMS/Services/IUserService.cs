using EWMS.DTOs;

public interface IUserService
{
    Task<UserDto?> ValidateUserAsync(string username, string password);
}