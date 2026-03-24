using EWMS.DTOs;
using EWMS.Models;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PurchaseOrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersAsync(int warehouseId, string? status = null)
        {
            // Removed automatic status update - status should be manually updated
            // await _unitOfWork.PurchaseOrders.UpdateToReadyToReceiveAsync(warehouseId);
            // await _unitOfWork.SaveChangesAsync();

            return await _unitOfWork.PurchaseOrders.GetByWarehouseIdAsync(warehouseId, status);
        }

        public async Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(int id, int warehouseId)
        {
            return await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(id, warehouseId);
        }

        public async Task<PurchaseOrder> CreatePurchaseOrderAsync(PurchaseOrderCreateViewModel model, int warehouseId, int userId)
        {
            // Calculate total amount from details
            decimal totalAmount = 0;
            foreach (var detail in model.Details)
            {
                if (detail.ProductId > 0 && detail.Quantity > 0 && detail.UnitPrice > 0)
                {
                    totalAmount += detail.Quantity * detail.UnitPrice;
                }
            }

            var purchaseOrder = new PurchaseOrder
            {
                SupplierId = model.SupplierId,
                WarehouseId = warehouseId,
                ExpectedReceivingDate = model.ExpectedReceivingDate ?? DateTime.Now.AddDays(7),
                CreatedBy = userId,
                Status = "Ordered",
                CreatedAt = DateTime.Now,
            };

            await _unitOfWork.PurchaseOrders.AddAsync(purchaseOrder);
            await _unitOfWork.SaveChangesAsync();

            // Group by ProductId to handle duplicate products (merge quantities)
            var groupedDetails = model.Details
                .Where(d => d.ProductId > 0 && d.Quantity > 0 && d.UnitPrice > 0)
                .GroupBy(d => d.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Quantity = g.Sum(d => d.Quantity),
                    UnitPrice = g.First().UnitPrice // Use first unit price for duplicates
                })
                .ToList();

            foreach (var detail in groupedDetails)
            {
                var orderDetail = new PurchaseOrderDetail
                {
                    PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                    ProductId = detail.ProductId,
                    Quantity = detail.Quantity,
                    UnitPrice = detail.UnitPrice
                    // TotalPrice is a computed column, don't set it
                };

                await _unitOfWork.PurchaseOrders.Context.PurchaseOrderDetails.AddAsync(orderDetail);
            }

            await _unitOfWork.SaveChangesAsync();
            return purchaseOrder;
        }

        // Removed: MarkAsDeliveredAsync - This method was doing nothing (keeping status as "Ordered")
        // Status is automatically updated when stock-in is performed via StockInService
        public async Task<bool> MarkAsDeliveredAsync(int id, int warehouseId)
        {
            // This endpoint is kept for backward compatibility but does nothing
            // The actual status update happens in StockInService when goods are received
            return await Task.FromResult(true);
        }

        public async Task<bool> CancelPurchaseOrderAsync(int id, int warehouseId, int userId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.FirstOrDefaultAsync(
                po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "Ordered")
                return false;

            // Chỉ cho phép người tạo đơn hủy
            if (purchaseOrder.CreatedBy != userId)
                return false;

            purchaseOrder.Status = "Cancelled";
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePurchaseOrderAsync(int id, int warehouseId, int userId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(id, warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "Ordered")
                return false;

            // Chỉ cho phép người tạo đơn xóa
            if (purchaseOrder.CreatedBy != userId)
                return false;

            _unitOfWork.PurchaseOrders.Delete(purchaseOrder);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ProductBySupplierDTO>> GetProductsBySupplierAsync(int supplierId)
        {
            // Query directly from DB with filter by SupplierId via Category to avoid loading all products
            var filtered = await _unitOfWork.Products.Context.Products
                .Include(p => p.Category)
                .Where(p => p.Category != null && p.Category.SupplierId == supplierId)
                .OrderBy(p => p.ProductName)
                .Select(p => new ProductBySupplierDTO
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.Category!.CategoryName,
                    CostPrice = p.CostPrice ?? 0
                })
                .ToListAsync();

            return filtered;
        }

        public async Task<IEnumerable<PurchaseOrderListDTO>> GetPurchaseOrderListAsync(int warehouseId, string? status, string? search)
        {
            // Removed automatic status update - status should be manually updated
            // await _unitOfWork.PurchaseOrders.UpdateToReadyToReceiveAsync(warehouseId);
            // await _unitOfWork.SaveChangesAsync();

            var purchaseOrders = await _unitOfWork.PurchaseOrders.GetByWarehouseIdAsync(warehouseId, status);

            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                purchaseOrders = purchaseOrders.Where(po =>
                    po.PurchaseOrderId.ToString().Contains(search) ||
                    po.Supplier.SupplierName.Contains(search));
            }

            return purchaseOrders.Select(po =>
            {
                var totalItems = po.PurchaseOrderDetails.Sum(d => d.Quantity);
                var receivedItems = po.StockInReceipts
                    .SelectMany(si => si.StockInDetails)
                    .Sum(sid => sid.Quantity);

                return new PurchaseOrderListDTO
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    SupplierName = po.Supplier.SupplierName,
                    ExpectedReceivingDate = po.ExpectedReceivingDate,
                    TotalItems = totalItems,
                    ReceivedItems = receivedItems,
                    RemainingItems = totalItems - receivedItems,
                    TotalAmount = po.PurchaseOrderDetails.Sum(d => d.TotalPrice ?? 0),
                    CreatedBy = po.CreatedByNavigation.FullName ?? po.CreatedByNavigation.Username,
                    Status = po.Status ?? "Unknown",
                    CreatedAt = po.CreatedAt
                };
            }).ToList();
        }
    }
}
