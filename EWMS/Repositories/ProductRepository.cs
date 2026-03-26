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
                .Include(p => p.ProductSuppliers)
                    .ThenInclude(ps => ps.Supplier)
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
                .Include(p => p.Inventories)
                .Include(p => p.ProductSuppliers)
                    .ThenInclude(ps => ps.Supplier)
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
                // Supplier filter now uses ProductSuppliers many-to-many table
                query = query.Where(p => p.ProductSuppliers.Any(ps => ps.SupplierId == supplierId.Value));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderBy(p => p.ProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (products, totalCount);
        }

        public async Task UpdateProductPricesByMovingAverageAsync(int productId, int quantityReceived, decimal unitPrice)
        {
            // Get product
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new Exception($"Product with ID {productId} not found");
            }

            // Get total current inventory BEFORE this receipt (inventory has not been updated yet)
            var currentTotalQuantity = await _context.Inventories
                .Where(i => i.ProductId == productId)
                .SumAsync(i => i.Quantity ?? 0);

            // Calculate new CostPrice using Moving Weighted Average formula
            decimal newCostPrice;
            
            if (currentTotalQuantity <= 0)
            {
                // If no stock before, new cost price = unit price of this receipt
                newCostPrice = unitPrice;
            }
            else
            {
                // Formula: (Current Stock × Current CostPrice + Quantity Received × Unit Price) / (Current Stock + Quantity Received)
                var currentCostPrice = product.CostPrice ?? 0;
                newCostPrice = (currentTotalQuantity * currentCostPrice + quantityReceived * unitPrice) / (currentTotalQuantity + quantityReceived);
            }

            // Round to 2 decimal places
            newCostPrice = Math.Round(newCostPrice, 2);

            // Calculate new SellingPrice (30% profit margin)
            var newSellingPrice = Math.Round(newCostPrice * 1.3m, 2);

            // Update product prices
            product.CostPrice = newCostPrice;
            product.SellingPrice = newSellingPrice;

            _context.Products.Update(product);
        }
    }
}
