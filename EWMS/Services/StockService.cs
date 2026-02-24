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

        public async Task<StockSummaryDTO> GetStockSummaryAsync(int warehouseId)
        {
            var locations = await _unitOfWork.Locations.GetByWarehouseIdAsync(warehouseId);
            var inventories = await _unitOfWork.Inventories.GetByWarehouseIdAsync(warehouseId);

            var totalLocations = locations.Count();
            var totalCapacity = locations.Sum(l => l.Capacity);
            var totalStock = inventories.Sum(i => i.Quantity ?? 0);
            var totalProducts = inventories.Where(i => i.Quantity > 0).Select(i => i.ProductId).Distinct().Count();

            var utilizationRate = totalCapacity > 0
                ? Math.Round((double)totalStock / totalCapacity * 100, 2)
                : 0;

            return new StockSummaryDTO
            {
                TotalLocations = totalLocations,
                TotalCapacity = totalCapacity,
                TotalStock = totalStock,
                TotalProducts = totalProducts,
                AvailableSpace = totalCapacity - totalStock,
                UtilizationRate = utilizationRate
            };
        }
    }
}
