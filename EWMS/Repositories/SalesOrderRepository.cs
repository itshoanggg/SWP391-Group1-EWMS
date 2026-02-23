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

        public async Task<(List<SalesOrder> Orders, int TotalCount)> GetSalesOrdersAsync(int warehouseId, string? customer, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize)
        {
            var query = _context.SalesOrders
                .Include(so => so.SalesOrderDetails)
                    .ThenInclude(sod => sod.Product)
                .Include(so => so.Warehouse)
                .Include(so => so.CreatedByNavigation)
                .Where(so => so.WarehouseId == warehouseId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(customer))
            {
                var keyword = customer.Trim().ToLower();
                query = query.Where(so => so.CustomerName.ToLower().Contains(keyword) || (so.CustomerPhone != null && so.CustomerPhone.ToLower().Contains(keyword)));
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(so => so.CreatedAt >= from);
            }
            if (toDate.HasValue)
            {
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(so => so.CreatedAt < toExclusive);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(so => so.Status == status);
            }

            var totalCount = await query.CountAsync();

            var skip = (page - 1) * pageSize;
            var orders = await query
                .OrderByDescending(so => so.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            return (orders, totalCount);
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

        public async Task<bool> UpdateSalesOrderStatusAsync(int salesOrderId, string status)
        {
            var salesOrder = await _context.SalesOrders.FindAsync(salesOrderId);
            if (salesOrder == null)
            {
                return false;
            }

            salesOrder.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(string? Name, string? Address)?> GetLatestCustomerByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            var record = await _context.SalesOrders
                .Where(o => o.CustomerPhone != null && o.CustomerPhone == phone)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new { o.CustomerName, o.CustomerAddress })
                .FirstOrDefaultAsync();

            if (record == null) return null;
            return (record.CustomerName, record.CustomerAddress);
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}