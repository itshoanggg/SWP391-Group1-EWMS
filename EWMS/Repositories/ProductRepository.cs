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
                    .ThenInclude(c => c.Supplier)
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

        // New methods for Product Management
        public async Task<List<ProductCategory>> GetAllCategoriesWithSupplierAsync()
        {
            return await _context.ProductCategories
                .Include(c => c.Supplier)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
        }

        public async Task<(List<Product> Products, int TotalCount)> GetProductsPagedAsync(
            int page, 
            int pageSize, 
            string? searchTerm, 
            int? categoryId, 
            int? supplierId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                    .ThenInclude(c => c.Supplier)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(p => 
                    p.ProductName.ToLower().Contains(term) || 
                    (p.Category != null && p.Category.CategoryName.ToLower().Contains(term)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (supplierId.HasValue)
            {
                query = query.Where(p => p.Category != null && p.Category.SupplierId == supplierId.Value);
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderBy(p => p.ProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (products, totalCount);
        }
    }
}
