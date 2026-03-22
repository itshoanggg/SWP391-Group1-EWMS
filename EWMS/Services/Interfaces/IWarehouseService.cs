using EWMS.ViewModels;

namespace EWMS.Services.Interfaces;

public interface IWarehouseService
{
    Task<WarehouseListViewModel> GetWarehousesAsync(string? searchQuery, int page, int pageSize);
    Task<WarehouseDetailsViewModel?> GetWarehouseDetailsAsync(int warehouseId, int page = 1, int pageSize = 10);
    Task<int> CreateWarehouseAsync(CreateWarehouseViewModel model);
    Task<bool> UpdateWarehouseAsync(EditWarehouseViewModel model);
    Task<bool> DeleteWarehouseAsync(int warehouseId);
    Task<EditWarehouseViewModel?> GetWarehouseForEditAsync(int warehouseId);
    
    Task<LocationDetailsViewModel?> GetLocationDetailsAsync(int locationId);
    Task<int> CreateLocationAsync(CreateLocationViewModel model);
    Task<bool> UpdateLocationAsync(EditLocationViewModel model);
    Task<bool> DeleteLocationAsync(int locationId);
    Task<EditLocationViewModel?> GetLocationForEditAsync(int locationId);
    Task<CreateLocationViewModel> PrepareCreateLocationViewModelAsync(int? preselectedWarehouseId = null);
}
