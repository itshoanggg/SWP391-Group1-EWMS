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
            await _unitOfWork.PurchaseOrders.UpdateToDeliveredAsync(warehouseId);
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
                Status = "InTransit",
                CreatedAt = DateTime.Now,
                ExpectedReceivingDate = model.ExpectedReceivingDate ?? DateTime.Now,
                TotalAmount = totalAmount  // ✅ Save TotalAmount
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

        public async Task<bool> MarkAsDeliveredAsync(int id, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.FirstOrDefaultAsync(
                po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "InTransit")
                return false;

            purchaseOrder.Status = "Delivered";
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelPurchaseOrderAsync(int id, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.FirstOrDefaultAsync(
                po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "InTransit")
                return false;

            purchaseOrder.Status = "Cancelled";
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePurchaseOrderAsync(int id, int warehouseId)
        {
            var purchaseOrder = await _unitOfWork.PurchaseOrders.GetByIdWithDetailsAsync(id, warehouseId);

            if (purchaseOrder == null || purchaseOrder.Status != "InTransit")
                return false;

            _unitOfWork.PurchaseOrders.Delete(purchaseOrder);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ProductBySupplierDTO>> GetProductsBySupplierAsync(int supplierId)
        {
            var products = await _unitOfWork.Products.GetAllWithCategoryAsync();

            return products.Select(p => new ProductBySupplierDTO
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryName = p.Category?.CategoryName ?? "N/A",
                CostPrice = p.CostPrice ?? 0
            }).ToList();
        }

        public async Task<IEnumerable<PurchaseOrderListDTO>> GetPurchaseOrderListAsync(int warehouseId, string? status, string? search)
        {
            await _unitOfWork.PurchaseOrders.UpdateToDeliveredAsync(warehouseId);
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
