using EWMS.DTOs;

namespace EWMS.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserDto?> ValidateUserAsync(string username, string password);
        Task<int> GetWarehouseIdByUserIdAsync(int userId);
        Task<string?> GetWarehouseNameByUserIdAsync(int userId);
        int GetCurrentUserId();
    }
}
