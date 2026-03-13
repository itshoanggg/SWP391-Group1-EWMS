using EWMS.Models;

namespace EWMS.Repositories.Interfaces
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        Task<IEnumerable<Product>> GetAllWithCategoryAsync();
        Task<Product?> GetByIdWithCategoryAsync(int id);
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int productId);
        Task<List<ProductCategory>> GetAllCategoriesWithSupplierAsync();
        Task<(List<Product> Products, int TotalCount)> GetProductsPagedAsync(int page, int pageSize, string? searchTerm, int? categoryId, int? supplierId);
    }
}
