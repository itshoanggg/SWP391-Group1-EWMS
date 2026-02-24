using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface ISupplierRepository : IGenericRepository<Supplier>
    {
        Task<IEnumerable<Supplier>> GetAllOrderedByNameAsync();
    }
}
