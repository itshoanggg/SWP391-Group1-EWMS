using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EWMS.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<int> GetWarehouseIdByUserIdAsync(int userId)
        {
            return await _unitOfWork.UserWarehouses.GetWarehouseIdByUserIdAsync(userId);
        }

        public int GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
                return 0;

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (idClaim == null)
                return 0;

            return int.TryParse(idClaim.Value, out var userId) ? userId : 0;
        }
    }
}
