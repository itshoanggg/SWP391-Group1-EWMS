using EWMS.DTOs;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services
{
    public class StockService : IStockService
    {
        private readonly IUnitOfWork _unitOfWork;

        public StockService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<RackDTO>> GetRacksAsync(int warehouseId)
        {
            var locations = await _unitOfWork.Locations.GetByWarehouseIdAsync(warehouseId);

            return locations
                .Where(l => l.Rack != null)
                .GroupBy(l => l.Rack)
                .Select(g => new RackDTO
                {
                    Rack = g.Key!,
                    LocationCount = g.Count(),
                    TotalCapacity = g.Sum(l => l.Capacity),
                    CurrentStock = g.SelectMany(l => l.Inventories).Sum(i => i.Quantity ?? 0)
                })
                .OrderBy(r => r.Rack)
                .ToList();
        }

        public async Task<IEnumerable<LocationDTO>> GetLocationsByRackAsync(int warehouseId, string rack)
        {
            var locations = await _unitOfWork.Locations.GetByRackAsync(warehouseId, rack);

            return locations.Select(l => new LocationDTO
            {
                LocationId = l.LocationId,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                Rack = l.Rack,
                Capacity = l.Capacity,
                CurrentStock = l.Inventories.Sum(i => i.Quantity ?? 0),
                ProductCount = l.Inventories.Count(i => i.Quantity > 0)
            }).ToList();
        }

        public async Task<IEnumerable<ProductInLocationDTO>> GetProductsByLocationAsync(int locationId)
        {
            var inventories = await _unitOfWork.Inventories.Context.Inventories
                .Include(i => i.Product)
                    .ThenInclude(p => p.Category)
                .Include(i => i.Location)
                .Where(i => i.LocationId == locationId && i.Quantity > 0)
                .ToListAsync();

            return inventories.Select(i => new ProductInLocationDTO
            {
                ProductId = i.ProductId,
                Sku = $"SKU-{i.ProductId:D5}",
                ProductName = i.Product.ProductName,
                CategoryName = i.Product.Category?.CategoryName ?? "N/A",
                Quantity = i.Quantity ?? 0,
                LocationCode = i.Location.LocationCode,
                LocationName = i.Location.LocationName,
                Rack = i.Location.Rack,
                LastUpdated = i.LastUpdated
            }).OrderBy(p => p.ProductName).ToList();
        }

        public async Task<bool> PerformInternalTransferAsync(int warehouseId, int fromLocationId, int toLocationId, int productId, int quantity, int userId, string? reason)
        {
            var dbContext = _unitOfWork.Inventories.Context;
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                // Verify source inventory
                var sourceInventory = await dbContext.Inventories
                    .FirstOrDefaultAsync(i => i.LocationId == fromLocationId && i.ProductId == productId);

                if (sourceInventory == null || sourceInventory.Quantity < quantity)
                    throw new Exception("Sản phẩm không đủ số lượng tại vị trí nguồn.");

                // Validate locations belong to warehouse
                var fromLocation = await dbContext.Locations.FirstOrDefaultAsync(l => l.LocationId == fromLocationId && l.WarehouseId == warehouseId);
                var toLocation = await dbContext.Locations.FirstOrDefaultAsync(l => l.LocationId == toLocationId && l.WarehouseId == warehouseId);

                if (fromLocation == null || toLocation == null)
                    throw new Exception("Vị trí không hợp lệ hoặc không thuộc kho hiện tại.");

                // Deduct from source
                sourceInventory.Quantity -= quantity;
                sourceInventory.LastUpdated = DateTime.Now;

                // Add to destination
                var destInventory = await dbContext.Inventories
                    .FirstOrDefaultAsync(i => i.LocationId == toLocationId && i.ProductId == productId);

                if (destInventory != null)
                {
                    destInventory.Quantity = (destInventory.Quantity ?? 0) + quantity;
                    destInventory.LastUpdated = DateTime.Now;
                }
                else
                {
                    destInventory = new Models.Inventory
                    {
                        LocationId = toLocationId,
                        ProductId = productId,
                        Quantity = quantity,
                        LastUpdated = DateTime.Now
                    };
                    dbContext.Inventories.Add(destInventory);
                }

                // Log Activity
                var log = new Models.ActivityLog
                {
                    UserId = userId,
                    Action = "Internal Transfer",
                    TableName = "Inventory",
                    RecordId = sourceInventory.InventoryId,
                    Description = $"Transferred {quantity} of product {productId} from {fromLocation.Rack}-{fromLocation.LocationCode} to {toLocation.Rack}-{toLocation.LocationCode}. Reason: {reason}",
                    CreatedAt = DateTime.Now
                };
                dbContext.ActivityLogs.Add(log);

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
