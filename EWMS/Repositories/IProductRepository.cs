using EWMS.Models;

namespace EWMS.Repositories
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int productId);
        Task<List<Product>> GetProductsByCategoryAsync(int categoryId);
    }
}
