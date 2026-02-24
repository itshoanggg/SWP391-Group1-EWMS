using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class SupplierRepository : GenericRepository<Supplier>, ISupplierRepository
    {
        public SupplierRepository(EWMSContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Supplier>> GetAllOrderedByNameAsync()
        {
            return await _dbSet
                .OrderBy(s => s.SupplierName)
                .ToListAsync();
        }
    }
}
