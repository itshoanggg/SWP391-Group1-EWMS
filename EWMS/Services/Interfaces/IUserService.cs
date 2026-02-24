namespace EWMS.Services.Interfaces
{
    public interface IUserService
    {
        Task<int> GetWarehouseIdByUserIdAsync(int userId);
        int GetCurrentUserId(); // TODO: Replace with actual authentication
    }
}
