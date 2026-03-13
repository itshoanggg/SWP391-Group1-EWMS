using EWMS.Models;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Services;

public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly EWMS.Repositories.ILocationRepository _locationRepository;
    private readonly EWMSDbContext _context;

    public WarehouseService(
        IWarehouseRepository warehouseRepository,
        EWMS.Repositories.ILocationRepository locationRepository,
        EWMSDbContext context)
    {
        _warehouseRepository = warehouseRepository;
        _locationRepository = locationRepository;
        _context = context;
    }

    public async Task<WarehouseListViewModel> GetWarehousesAsync(string? searchQuery, int page, int pageSize)
    {
        var (warehouses, totalCount) = await _warehouseRepository.GetWarehousesPagedAsync(page, pageSize, searchQuery);

        var warehouseItems = warehouses.Select(w =>
        {
            var totalCapacity = w.Locations.Sum(l => l.Capacity);
            var totalUsed = w.Locations.Sum(l => 
                l.Inventories?.Sum(i => i.Quantity ?? 0) ?? 0);
            var usagePercentage = totalCapacity > 0 ? (int)Math.Round((double)totalUsed / totalCapacity * 100) : 0;

            // Generate Warehouse Code and Prefix
            var warehouseCode = $"WH-{w.WarehouseId:D3}";
            var prefix = GeneratePrefixFromName(w.WarehouseName);

            return new WarehouseItemViewModel
            {
                WarehouseId = w.WarehouseId,
                WarehouseName = w.WarehouseName,
                Address = w.Address,
                WarehouseCode = warehouseCode,
                Prefix = prefix,
                LocationCount = w.Locations.Count,
                TotalCapacity = totalCapacity,
                TotalUsed = totalUsed,
                UsagePercentage = usagePercentage,
                CreatedAt = w.CreatedAt
            };
        }).ToList();

        return new WarehouseListViewModel
        {
            Warehouses = warehouseItems,
            SearchQuery = searchQuery,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TotalCount = totalCount
        };
    }

    public async Task<WarehouseDetailsViewModel?> GetWarehouseDetailsAsync(int warehouseId, int page = 1, int pageSize = 10)
    {
        var warehouse = await _warehouseRepository.GetWarehouseWithLocationsAsync(warehouseId);
        if (warehouse == null) return null;

        var totalCapacity = warehouse.Locations.Sum(l => l.Capacity);
        var totalUsed = warehouse.Locations.Sum(l => 
            l.Inventories?.Sum(i => i.Quantity ?? 0) ?? 0);
        var usagePercentage = totalCapacity > 0 ? (int)Math.Round((double)totalUsed / totalCapacity * 100) : 0;

        var warehouseCode = $"WH-{warehouse.WarehouseId:D3}";
        var prefix = GeneratePrefixFromName(warehouse.WarehouseName);

        // Get all locations first for mapping
        var allLocationItems = warehouse.Locations.Select(l =>
        {
            var used = l.Inventories?.Sum(i => i.Quantity ?? 0) ?? 0;
            var locationUsagePercentage = l.Capacity > 0 ? (int)Math.Round((double)used / l.Capacity * 100) : 0;

            return new LocationItemViewModel
            {
                LocationId = l.LocationId,
                WarehouseId = l.WarehouseId,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                Rack = l.Rack,
                Capacity = l.Capacity,
                Used = used,
                UsagePercentage = locationUsagePercentage
            };
        }).OrderBy(l => l.LocationCode).ToList();

        // Apply pagination
        var totalLocations = allLocationItems.Count;
        var totalPages = (int)Math.Ceiling(totalLocations / (double)pageSize);
        var paginatedLocations = allLocationItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new WarehouseDetailsViewModel
        {
            WarehouseId = warehouse.WarehouseId,
            WarehouseName = warehouse.WarehouseName,
            Address = warehouse.Address,
            WarehouseCode = warehouseCode,
            Prefix = prefix,
            CreatedAt = warehouse.CreatedAt,
            LocationCount = totalLocations,
            TotalCapacity = totalCapacity,
            TotalUsed = totalUsed,
            UsagePercentage = usagePercentage,
            Locations = paginatedLocations,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<int> CreateWarehouseAsync(CreateWarehouseViewModel model)
    {
        var warehouse = new Warehouse
        {
            WarehouseName = model.WarehouseName,
            Address = model.Address,
            CreatedAt = DateTime.Now
        };

        await _warehouseRepository.AddAsync(warehouse);
        await _context.SaveChangesAsync();

        // Create locations if provided
        if (model.Locations != null && model.Locations.Any())
        {
            foreach (var locModel in model.Locations)
            {
                var location = new Location
                {
                    WarehouseId = warehouse.WarehouseId,
                    LocationCode = locModel.LocationCode,
                    LocationName = locModel.LocationName,
                    Rack = locModel.Rack,
                    Capacity = locModel.Capacity
                };

                await _context.Locations.AddAsync(location);
            }
            await _context.SaveChangesAsync();
        }

        return warehouse.WarehouseId;
    }

    public async Task<bool> UpdateWarehouseAsync(EditWarehouseViewModel model)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(model.WarehouseId);
        if (warehouse == null) return false;

        warehouse.WarehouseName = model.WarehouseName;
        warehouse.Address = model.Address;

        _context.Warehouses.Update(warehouse);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteWarehouseAsync(int warehouseId)
    {
        var warehouse = await _warehouseRepository.GetWarehouseWithLocationsAsync(warehouseId);
        if (warehouse == null) return false;

        // Check if warehouse has locations with inventory
        var hasInventory = warehouse.Locations.Any(l => l.Inventories != null && l.Inventories.Any(i => (i.Quantity ?? 0) > 0));
        if (hasInventory)
        {
            throw new InvalidOperationException("Cannot delete warehouse with inventory!");
        }

        // Delete all locations first
        foreach (var location in warehouse.Locations)
        {
            _context.Locations.Remove(location);
        }

        _context.Warehouses.Remove(warehouse);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<EditWarehouseViewModel?> GetWarehouseForEditAsync(int warehouseId)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId);
        if (warehouse == null) return null;

        var prefix = GeneratePrefixFromName(warehouse.WarehouseName);

        return new EditWarehouseViewModel
        {
            WarehouseId = warehouse.WarehouseId,
            WarehouseName = warehouse.WarehouseName,
            Address = warehouse.Address ?? string.Empty,
            Prefix = prefix
        };
    }

    // Location methods
    public async Task<LocationListViewModel> GetLocationsAsync(string? searchQuery, int? warehouseId, int page, int pageSize)
    {
        var (locations, totalCount) = await _locationRepository.GetLocationsPagedAsync(page, pageSize, searchQuery, warehouseId);

        var locationItems = locations.Select(l =>
        {
            var used = l.Inventories?.Sum(i => i.Quantity ?? 0) ?? 0;
            var usagePercentage = l.Capacity > 0 ? (int)Math.Round((double)used / l.Capacity * 100) : 0;

            return new LocationItemViewModel
            {
                LocationId = l.LocationId,
                WarehouseId = l.WarehouseId,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                Rack = l.Rack,
                Capacity = l.Capacity,
                Used = used,
                UsagePercentage = usagePercentage,
                WarehouseName = l.Warehouse?.WarehouseName
            };
        }).ToList();

        var warehouses = await _warehouseRepository.GetAllWarehousesAsync();
        var warehouseSelectItems = warehouses.Select(w => new WarehouseSelectItem
        {
            WarehouseId = w.WarehouseId,
            WarehouseName = w.WarehouseName
        }).ToList();

        return new LocationListViewModel
        {
            Locations = locationItems,
            SearchQuery = searchQuery,
            FilterWarehouseId = warehouseId,
            Warehouses = warehouseSelectItems,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TotalCount = totalCount
        };
    }

    public async Task<LocationDetailsViewModel?> GetLocationDetailsAsync(int locationId)
    {
        var location = await _locationRepository.GetLocationWithInventoryAsync(locationId);
        if (location == null) return null;

        var used = location.Inventories?.Sum(i => i.Quantity ?? 0) ?? 0;
        var available = location.Capacity - used;
        var usagePercentage = location.Capacity > 0 ? (int)Math.Round((double)used / location.Capacity * 100) : 0;

        var inventoryItems = location.Inventories?
            .Where(i => (i.Quantity ?? 0) > 0)
            .Select(i => new InventoryItemViewModel
            {
                ProductName = i.Product?.ProductName ?? "Unknown",
                CategoryName = i.Product?.Category?.CategoryName,
                Unit = i.Product?.Unit,
                Quantity = i.Quantity ?? 0,
                LastUpdated = i.LastUpdated
            }).ToList() ?? new List<InventoryItemViewModel>();

        return new LocationDetailsViewModel
        {
            LocationId = location.LocationId,
            WarehouseId = location.WarehouseId,
            LocationCode = location.LocationCode,
            LocationName = location.LocationName,
            Rack = location.Rack,
            Capacity = location.Capacity,
            Used = used,
            Available = available,
            UsagePercentage = usagePercentage,
            WarehouseName = location.Warehouse?.WarehouseName ?? "Unknown",
            InventoryItems = inventoryItems
        };
    }

    public async Task<int> CreateLocationAsync(CreateLocationViewModel model)
    {
        // Check if location code already exists in this warehouse
        var exists = await _locationRepository.LocationCodeExistsAsync(model.LocationCode, model.WarehouseId);
        if (exists)
        {
            throw new InvalidOperationException($"Location code '{model.LocationCode}' already exists in this warehouse!");
        }

        var location = new Location
        {
            WarehouseId = model.WarehouseId,
            LocationCode = model.LocationCode,
            LocationName = model.LocationName,
            Rack = model.Rack,
            Capacity = model.Capacity
        };

        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        return location.LocationId;
    }

    public async Task<bool> UpdateLocationAsync(EditLocationViewModel model)
    {
        var location = await _context.Locations.FindAsync(model.LocationId);
        if (location == null) return false;

        // Check if changing location code conflicts with existing ones
        if (location.LocationCode != model.LocationCode)
        {
            var exists = await _locationRepository.LocationCodeExistsAsync(model.LocationCode, location.WarehouseId, model.LocationId);
            if (exists)
            {
                throw new InvalidOperationException($"Location code '{model.LocationCode}' already exists in this warehouse!");
            }
        }

        // Validate capacity is not less than used
        if (model.Capacity < model.CurrentUsed)
        {
            throw new InvalidOperationException($"Capacity must be >= {model.CurrentUsed} (currently used)!");
        }

        location.LocationCode = model.LocationCode;
        location.LocationName = model.LocationName;
        location.Rack = model.Rack;
        location.Capacity = model.Capacity;

        _context.Locations.Update(location);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteLocationAsync(int locationId)
    {
        var location = await _locationRepository.GetLocationWithInventoryAsync(locationId);
        if (location == null) return false;

        // Check if location has inventory
        var hasInventory = location.Inventories != null && location.Inventories.Any(i => (i.Quantity ?? 0) > 0);
        if (hasInventory)
        {
            throw new InvalidOperationException("Cannot delete location with inventory!");
        }

        _context.Locations.Remove(location);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<EditLocationViewModel?> GetLocationForEditAsync(int locationId)
    {
        var location = await _context.Locations.FindAsync(locationId);
        if (location == null) return null;

        var currentUsed = await _locationRepository.GetLocationUsedCapacityAsync(locationId);

        return new EditLocationViewModel
        {
            LocationId = location.LocationId,
            WarehouseId = location.WarehouseId,
            LocationCode = location.LocationCode,
            LocationName = location.LocationName,
            Rack = location.Rack ?? string.Empty,
            Capacity = location.Capacity,
            CurrentUsed = currentUsed
        };
    }

    public async Task<CreateLocationViewModel> PrepareCreateLocationViewModelAsync(int? preselectedWarehouseId = null)
    {
        var warehouses = await _warehouseRepository.GetAllWarehousesAsync();
        var warehouseSelectItems = warehouses.Select(w => new WarehouseSelectItem
        {
            WarehouseId = w.WarehouseId,
            WarehouseName = w.WarehouseName
        }).ToList();

        return new CreateLocationViewModel
        {
            WarehouseId = preselectedWarehouseId ?? 0,
            Warehouses = warehouseSelectItems
        };
    }

    // Helper method to generate prefix from warehouse name
    private string GeneratePrefixFromName(string warehouseName)
    {
        if (string.IsNullOrWhiteSpace(warehouseName)) return "WH";

        var words = warehouseName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length >= 2)
        {
            return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpper();
        }
        else if (words.Length == 1 && words[0].Length >= 3)
        {
            return words[0].Substring(0, 3).ToUpper();
        }
        else
        {
            return words[0].ToUpper();
        }
    }
}
