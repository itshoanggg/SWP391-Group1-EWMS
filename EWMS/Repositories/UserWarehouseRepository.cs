using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class UserWarehouseRepository : GenericRepository<UserWarehouse>, IUserWarehouseRepository
    {
        public UserWarehouseRepository(EWMSDbContext context) : base(context)
        {
        }

        public async Task<int> GetWarehouseIdByUserIdAsync(int userId)
        {
            var userWarehouse = await _dbSet
                .Where(uw => uw.UserId == userId)
                .FirstOrDefaultAsync();

            return userWarehouse?.WarehouseId ?? 0;
        }
    }
}
