using EWMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class SalesOrderRepository : ISalesOrderRepository
    {
        private readonly EWMSDbContext _context;

        public SalesOrderRepository(EWMSDbContext context)
        {
            _context = context;
        }

        public async Task<List<SalesOrder>> GetSalesOrdersByWarehouseAsync(int warehouseId)
        {
            return await _context.SalesOrders
                .Include(so => so.SalesOrderDetails)
                    .ThenInclude(sod => sod.Product)
                .Include(so => so.Warehouse)
                .Include(so => so.CreatedByNavigation)
                .Where(so => so.WarehouseId == warehouseId)
                .OrderByDescending(so => so.CreatedAt)
                .ToListAsync();
        }

        public async Task<SalesOrder?> GetSalesOrderByIdAsync(int salesOrderId)
        {
            return await _context.SalesOrders
                .Include(so => so.SalesOrderDetails)
                    .ThenInclude(sod => sod.Product)
                .Include(so => so.Warehouse)
                .Include(so => so.CreatedByNavigation)
                .FirstOrDefaultAsync(so => so.SalesOrderId == salesOrderId);
        }

        public async Task<SalesOrder> CreateSalesOrderAsync(SalesOrder salesOrder)
        {
            _context.SalesOrders.Add(salesOrder);
            await _context.SaveChangesAsync();
            return salesOrder;
        }

        public async Task UpdateSalesOrderAsync(SalesOrder salesOrder)
        {
            _context.SalesOrders.Update(salesOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}