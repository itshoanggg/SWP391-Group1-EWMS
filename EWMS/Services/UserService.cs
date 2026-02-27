using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EWMS.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly EWMSDbContext _context;

        public UserService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor, EWMSDbContext context)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public async Task<int> GetWarehouseIdByUserIdAsync(int userId)
        {
            return await _unitOfWork.UserWarehouses.GetWarehouseIdByUserIdAsync(userId);
        }

        public async Task<string?> GetWarehouseNameByUserIdAsync(int userId)
        {
            var warehouseId = await _unitOfWork.UserWarehouses.GetWarehouseIdByUserIdAsync(userId);
            if (warehouseId == 0)
                return null;

            var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(warehouseId);
            return warehouse?.WarehouseName;
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

        public async Task<UserDto?> ValidateUserAsync(string username, string password)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.UserWarehouses)
                    .ThenInclude(uw => uw.Warehouse)
                .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == password && u.IsActive == true);

            if (user == null)
                return null;

            var warehouse = user.UserWarehouses.FirstOrDefault()?.Warehouse;

            return new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName ?? user.Username,
                Email = user.Email,
                RoleName = user.Role?.RoleName ?? string.Empty,
                WarehouseId = warehouse?.WarehouseId ?? 0,
                WarehouseName = warehouse?.WarehouseName ?? string.Empty
            };
        }
    }
}
