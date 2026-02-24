using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class ProductRepository : GenericRepository<Product>,
        EWMS.Repositories.Interfaces.IProductRepository,
        EWMS.Repositories.IProductRepository
    {
        public ProductRepository(EWMSDbContext context) : base(context)
        {
        }

        // Legacy methods used via UnitOfWork
        public async Task<IEnumerable<Product>> GetAllWithCategoryAsync()
        {
            return await _dbSet
                .Include(p => p.Category)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }

        public async Task<Product?> GetByIdWithCategoryAsync(int id)
        {
            return await _dbSet
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);
        }

        // New (master) methods used by Sales/InventoryCheck flows
        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int productId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == productId);
        }

        public async Task<List<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == categoryId)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }
    }
}
