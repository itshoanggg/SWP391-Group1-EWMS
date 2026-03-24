using EWMS.Models;
using EWMS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    // Implement both interface sets so legacy UoW features and new services both work
    public class InventoryRepository : GenericRepository<Inventory>,
        EWMS.Repositories.Interfaces.IInventoryRepository,
        EWMS.Repositories.IInventoryRepository
    {
        public InventoryRepository(EWMSDbContext context) : base(context)
        {
        }

        // Legacy (Interfaces) methods
        public async Task<Inventory?> GetByProductAndLocationAsync(int productId, int locationId)
        {
            return await _dbSet.FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);
        }

        public async Task<IEnumerable<Inventory>> GetByWarehouseIdAsync(int warehouseId)
        {
            return await _dbSet
                .Include(i => i.Product)
                    .ThenInclude(p => p.Category)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Inventory>> GetLowStockAsync(int warehouseId, int threshold)
        {
            return await _dbSet
                .Include(i => i.Product)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId && (i.Quantity ?? 0) <= threshold)
                .OrderBy(i => i.Quantity)
                .ToListAsync();
        }

        public async Task<IEnumerable<Inventory>> GetInventoryByProductIdAsync(int productId)
        {
            return await _dbSet
                .Include(i => i.Product)
                    .ThenInclude(p => p.Category)
                .Include(i => i.Location)
                    .ThenInclude(l => l.Warehouse)
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.Location.Warehouse.WarehouseName)
                    .ThenBy(i => i.Location.LocationName)
                .ToListAsync();
        }

        public async Task<int> GetCurrentStockAsync(int productId, int warehouseId)
        {
            var totalStock = await _context.Inventories
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId && i.Location.WarehouseId == warehouseId)
                .SumAsync(i => i.Quantity ?? 0);
            return totalStock;
        }


        public async Task<int> GetPendingOutgoingAsync(int productId, int warehouseId)
        {
            var pendingFromSales = await _context.SalesOrderDetails
                .Include(sod => sod.SalesOrder)
                .Where(sod => sod.ProductId == productId
                    && sod.SalesOrder.WarehouseId == warehouseId
                    && sod.SalesOrder.Status == "Pending"
                    && !_context.StockOutReceipts.Any(sor => sor.SalesOrderId == sod.SalesOrderId))
                .SumAsync(sod => sod.Quantity);

            var pendingFromTransfer = await _context.TransferDetails
                .Include(td => td.Transfer)
                .Where(td => td.ProductId == productId
                    && td.Transfer.FromWarehouseId == warehouseId
                    && td.Transfer.Status == "Approved"
                    && !_context.StockOutReceipts.Any(sor => sor.TransferId == td.TransferId))
                .SumAsync(td => td.Quantity);

            return pendingFromSales + pendingFromTransfer;
        }

        public async Task<List<Inventory>> GetInventoryByWarehouseAsync(int warehouseId)
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .Include(i => i.Location)
                .Where(i => i.Location.WarehouseId == warehouseId)
                .ToListAsync();
        }

        public async Task<Inventory?> GetInventoryByProductAndLocationAsync(int productId, int locationId)
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .Include(i => i.Location)
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.LocationId == locationId);
        }

        /// <summary>
        /// Lock và lấy available stock cho danh sách products.
        /// Sử dụng UPDLOCK, ROWLOCK để tránh race condition.
        /// QUAN TRỌNG: Method này PHẢI được gọi trong một transaction!
        /// 
        /// LOGIC: Available Stock = Current Stock - Pending Outgoing
        /// - Current Stock: Tổng số lượng hiện có trong kho (tất cả locations)
        /// - Pending Outgoing: Sales Orders + Transfer Requests chưa xuất kho
        /// - NOTE: KHÔNG tính Purchase Orders/Transfers đang chờ nhập kho (Conservative approach)
        ///         Lý do: Không có hệ thống pre-order, tránh oversell nếu supplier delay
        /// </summary>
        public async Task<Dictionary<int, int>> GetAndLockAvailableStockAsync(List<int> productIds, int warehouseId)
        {
            if (productIds == null || !productIds.Any())
            {
                return new Dictionary<int, int>();
            }

            // ════════════════════════════════════════════════════════════════════
            // BƯỚC 1: Lock inventory rows với FromSqlRaw + UPDLOCK, ROWLOCK
            // Chỉ lock các rows của products trong danh sách này
            // ════════════════════════════════════════════════════════════════════
            
            // Build parameterized SQL để tránh SQL injection
            var parameters = new List<object> { warehouseId };
            var paramPlaceholders = new List<string>();
            
            for (int i = 0; i < productIds.Count; i++)
            {
                paramPlaceholders.Add($"{{{i + 1}}}");  // {1}, {2}, {3}...
                parameters.Add(productIds[i]);
            }
            
            var sql = $@"
                SELECT i.*
                FROM Inventory i WITH (UPDLOCK, ROWLOCK)
                INNER JOIN Locations l ON i.LocationId = l.LocationId
                WHERE i.ProductId IN ({string.Join(",", paramPlaceholders)})
                  AND l.WarehouseId = {{0}}";
            
            // Lock các rows và load vào memory
            var lockedInventories = await _context.Inventories
                .FromSqlRaw(sql, parameters.ToArray())
                .Include(i => i.Location)
                .ToListAsync();

            // ════════════════════════════════════════════════════════════════════
            // BƯỚC 2: Tính available stock cho từng product
            // Available = CurrentStock - PendingOutgoing
            // ════════════════════════════════════════════════════════════════════
            
            var result = new Dictionary<int, int>();
            
            foreach (var productId in productIds)
            {
                // Current stock (từ locked inventories)
                var currentStock = lockedInventories
                    .Where(i => i.ProductId == productId)
                    .Sum(i => i.Quantity ?? 0);
                
                // Pending outgoing (Sales Orders chưa được fulfill)
                var pendingFromSales = await _context.SalesOrderDetails
                    .Where(sod => sod.ProductId == productId
                        && sod.SalesOrder.WarehouseId == warehouseId
                        && sod.SalesOrder.Status == "Pending"
                        && !_context.StockOutReceipts.Any(sor => sor.SalesOrderId == sod.SalesOrderId))
                    .SumAsync(sod => sod.Quantity);
                
                // Pending outgoing (Transfer Requests chưa được fulfill)
                var pendingFromTransfer = await _context.TransferDetails
                    .Where(td => td.ProductId == productId
                        && td.Transfer.FromWarehouseId == warehouseId
                        && td.Transfer.Status == "Approved"
                        && !_context.StockOutReceipts.Any(sor => sor.TransferId == td.TransferId))
                    .SumAsync(td => td.Quantity);
                
                var totalPendingOutgoing = pendingFromSales + pendingFromTransfer;
                var availableStock = currentStock - totalPendingOutgoing;
                
                result[productId] = availableStock;
            }
            
            return result;
        }
    }
}
