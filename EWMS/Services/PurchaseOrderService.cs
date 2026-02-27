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
            await _unitOfWork.PurchaseOrders.UpdateToReadyToReceiveAsync(warehouseId);
            await _unitOfWork.SaveChangesAsync();

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
                CreatedBy = userId,
                Status = "Ordered",
                CreatedAt = DateTime.Now,
            };

            await _unitOfWork.PurchaseOrders.AddAsync(purchaseOrder);
            await _unitOfWork.SaveChangesAsync();

            foreach (var detail in model.Details)
            {
                if (detail.ProductId > 0 && detail.Quantity > 0 && detail.UnitPrice > 0)
                {
                    var orderDetail = new PurchaseOrderDetail
                    {
                        PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        UnitPrice = detail.UnitPrice,
                        TotalPrice = detail.Quantity * detail.UnitPrice  // ✅ Calculate TotalPrice
                    };

                    await _unitOfWork.PurchaseOrders.Context.PurchaseOrderDetails.AddAsync(orderDetail);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            return purchaseOrder;
        }

        public async Task<bool> MarkAsDeliveredAsync(int id, int warehouseId) // now marks as ReadyToReceive
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.FirstOrDefaultAsync(
                po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "Ordered")
                return false;

            purchaseOrder.Status = "ReadyToReceive";
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelPurchaseOrderAsync(int id, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.FirstOrDefaultAsync(
                po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "Ordered")
                return false;

            purchaseOrder.Status = "Cancelled";
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePurchaseOrderAsync(int id, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(id, warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "Ordered")
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
            await _unitOfWork.PurchaseOrders.UpdateToReadyToReceiveAsync(warehouseId);
            await _unitOfWork.SaveChangesAsync();

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
                    ExpectedReceivingDate = po.CreatedAt, // Use CreatedAt instead
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
