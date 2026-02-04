using EWMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly EWMSDbContext _context;

        public InventoryRepository(EWMSDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetCurrentStockAsync(int productId, int warehouseId)
        {
            // Tổng số lượng tồn kho hiện tại của sản phẩm trong tất cả vị trí của kho
            var totalStock = await _context.Inventories
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId && i.Location.WarehouseId == warehouseId)
                .SumAsync(i => i.Quantity ?? 0);

            return totalStock;
        }

        public async Task<int> GetExpectedIncomingAsync(int productId, int warehouseId, DateTime beforeDate)
        {
            // Tổng số lượng dự kiến nhập từ:
            // 1. Purchase Orders đã approved, chưa nhận hàng, ngày dự kiến nhận < ngày xuất
            var expectedFromPurchase = await _context.PurchaseOrderDetails
                .Include(pod => pod.PurchaseOrder)
                .Where(pod => pod.ProductId == productId
                    && pod.PurchaseOrder.WarehouseId == warehouseId
                    && pod.PurchaseOrder.Status == "Approved"
                    && pod.PurchaseOrder.ExpectedReceivingDate < beforeDate
                    && !_context.StockInReceipts.Any(sir => sir.PurchaseOrderId == pod.PurchaseOrderId))
                .SumAsync(pod => pod.Quantity);

            // 2. Transfer Requests đã approved, chưa nhận hàng, đến kho này
            var expectedFromTransfer = await _context.TransferDetails
                .Include(td => td.Transfer)
                .Where(td => td.ProductId == productId
                    && td.Transfer.ToWarehouseId == warehouseId
                    && td.Transfer.Status == "Approved"
                    && td.Transfer.ApprovedDate < beforeDate
                    && !_context.StockInReceipts.Any(sir => sir.TransferId == td.TransferId))
                .SumAsync(td => td.Quantity);

            return expectedFromPurchase + expectedFromTransfer;
        }

        public async Task<int> GetPendingOutgoingAsync(int productId, int warehouseId)
        {
            // Tổng số lượng hàng chờ xuất kho từ:
            // 1. Sales Orders đã được tạo nhưng chưa xuất kho
            var pendingFromSales = await _context.SalesOrderDetails
                .Include(sod => sod.SalesOrder)
                .Where(sod => sod.ProductId == productId
                    && sod.SalesOrder.WarehouseId == warehouseId
                    && sod.SalesOrder.Status == "Pending"
                    && !_context.StockOutReceipts.Any(sor => sor.SalesOrderId == sod.SalesOrderId))
                .SumAsync(sod => sod.Quantity);

            // 2. Transfer Requests đã approved, chưa xuất kho, từ kho này
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
    }
}
