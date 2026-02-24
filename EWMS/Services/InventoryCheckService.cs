using EWMS.DTOs;
using EWMS.Repositories;

namespace EWMS.Services
{
    public class InventoryCheckService : IInventoryCheckService
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductRepository _productRepository;

        public InventoryCheckService(
            IInventoryRepository inventoryRepository,
            IProductRepository productRepository)
        {
            _inventoryRepository = inventoryRepository;
            _productRepository = productRepository;
        }

        public async Task<InventoryCheckResult> CheckInventoryAvailabilityAsync(InventoryCheckRequest request)
        {
            var result = new InventoryCheckResult
            {
                IsValid = true,
                CheckDetails = new List<InventoryCheckDto>()
            };

            foreach (var productRequest in request.Products)
            {
                var product = await _productRepository.GetProductByIdAsync(productRequest.ProductId);
                if (product == null)
                {
                    result.IsValid = false;
                    result.Message = $"Product not found with ID {productRequest.ProductId}";
                    continue;
                }

                var currentStock = await _inventoryRepository.GetCurrentStockAsync(
                    productRequest.ProductId,
                    request.WarehouseId);

                var expectedIncoming = await _inventoryRepository.GetExpectedIncomingAsync(
                    productRequest.ProductId,
                    request.WarehouseId,
                    request.ExpectedDeliveryDate);

                var pendingOutgoing = await _inventoryRepository.GetPendingOutgoingAsync(
                    productRequest.ProductId,
                    request.WarehouseId);

                var availableStock = currentStock + expectedIncoming - pendingOutgoing;
                var isAvailable = availableStock >= productRequest.Quantity;

                if (!isAvailable)
                {
                    result.IsValid = false;
                }

                result.CheckDetails.Add(new InventoryCheckDto
                {
                    ProductId = productRequest.ProductId,
                    ProductName = product.ProductName,
                    RequestedQuantity = productRequest.Quantity,
                    CurrentStock = currentStock,
                    ExpectedIncoming = expectedIncoming,
                    PendingOutgoing = pendingOutgoing,
                    AvailableStock = availableStock,
                    IsAvailable = isAvailable
                });
            }

            if (!result.IsValid && string.IsNullOrEmpty(result.Message))
            {
                result.Message = "Insufficient stock! Please check the quantity.";
            }
            else if (result.IsValid)
            {
                result.Message = "Enough goods to export.";
            }

            return result;
        }
    }
}