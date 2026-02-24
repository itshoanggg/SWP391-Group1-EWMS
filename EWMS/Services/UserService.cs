using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;

namespace EWMS.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<int> GetWarehouseIdByUserIdAsync(int userId)
        {
            return await _unitOfWork.UserWarehouses.GetWarehouseIdByUserIdAsync(userId);
        }

        public int GetCurrentUserId()
        {
            // TODO: Replace with actual authentication from HttpContext
            return 4;
        }
    }
}
