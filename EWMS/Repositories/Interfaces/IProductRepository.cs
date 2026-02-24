using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        Task<IEnumerable<Product>> GetAllWithCategoryAsync();
        Task<Product?> GetByIdWithCategoryAsync(int id);
    }
}
