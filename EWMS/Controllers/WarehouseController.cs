using EWMS.Services.Interfaces;
using EWMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EWMS.Controllers;

[Authorize(Roles = "Warehouse Manager")]
public class WarehouseController : Controller
{
    private readonly IWarehouseService _warehouseService;

    public WarehouseController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    // GET: Warehouse/Index
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        const int pageSize = 9; // 3x3 grid
        var viewModel = await _warehouseService.GetWarehousesAsync(search, page, pageSize);
        return View(viewModel);
    }

    // GET: Warehouse/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        const int pageSize = 10;
        var viewModel = await _warehouseService.GetWarehouseDetailsAsync(id, page, pageSize);
        if (viewModel == null)
        {
            TempData["ErrorMessage"] = "Warehouse not found!";
            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    // GET: Warehouse/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateWarehouseViewModel());
    }

    // POST: Warehouse/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWarehouseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var warehouseId = await _warehouseService.CreateWarehouseAsync(model);
            TempData["SuccessMessage"] = $"Warehouse '{model.WarehouseName}' created successfully!";
            return RedirectToAction(nameof(Details), new { id = warehouseId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error creating warehouse: {ex.Message}";
            return View(model);
        }
    }

    // GET: Warehouse/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var viewModel = await _warehouseService.GetWarehouseForEditAsync(id);
        if (viewModel == null)
        {
            TempData["ErrorMessage"] = "Warehouse not found!";
            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    // POST: Warehouse/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditWarehouseViewModel model)
    {
        if (id != model.WarehouseId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _warehouseService.UpdateWarehouseAsync(model);
            if (!result)
            {
                TempData["ErrorMessage"] = "Warehouse not found!";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Warehouse information updated successfully!";
            return RedirectToAction(nameof(Details), new { id = model.WarehouseId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error updating warehouse: {ex.Message}";
            return View(model);
        }
    }

    // POST: Warehouse/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var result = await _warehouseService.DeleteWarehouseAsync(id);
            if (!result)
            {
                TempData["ErrorMessage"] = "Warehouse not found!";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Warehouse deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting warehouse: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // GET: Warehouse/LocationList
    [HttpGet]
    public async Task<IActionResult> LocationList(string? search, int? warehouseId, int page = 1)
    {
        const int pageSize = 10;
        var viewModel = await _warehouseService.GetLocationsAsync(search, warehouseId, page, pageSize);
        return View(viewModel);
    }

    // GET: Warehouse/LocationDetails/5
    [HttpGet]
    public async Task<IActionResult> LocationDetails(int id)
    {
        var viewModel = await _warehouseService.GetLocationDetailsAsync(id);
        if (viewModel == null)
        {
            TempData["ErrorMessage"] = "Location not found!";
            return RedirectToAction(nameof(LocationList));
        }

        return View(viewModel);
    }

    // GET: Warehouse/CreateLocation
    [HttpGet]
    public async Task<IActionResult> CreateLocation(int? warehouseId)
    {
        var viewModel = await _warehouseService.PrepareCreateLocationViewModelAsync(warehouseId);
        return View(viewModel);
    }

    // POST: Warehouse/CreateLocation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLocation(CreateLocationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model = await _warehouseService.PrepareCreateLocationViewModelAsync(model.WarehouseId);
            return View(model);
        }

        try
        {
            var locationId = await _warehouseService.CreateLocationAsync(model);
            TempData["SuccessMessage"] = $"Location '{model.LocationCode}' created successfully!";
            return RedirectToAction(nameof(LocationDetails), new { id = locationId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("LocationCode", ex.Message);
            model = await _warehouseService.PrepareCreateLocationViewModelAsync(model.WarehouseId);
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error creating location: {ex.Message}";
            model = await _warehouseService.PrepareCreateLocationViewModelAsync(model.WarehouseId);
            return View(model);
        }
    }

    // GET: Warehouse/EditLocation/5
    [HttpGet]
    public async Task<IActionResult> EditLocation(int id)
    {
        var viewModel = await _warehouseService.GetLocationForEditAsync(id);
        if (viewModel == null)
        {
            TempData["ErrorMessage"] = "Location not found!";
            return RedirectToAction(nameof(LocationList));
        }

        return View(viewModel);
    }

    // POST: Warehouse/EditLocation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLocation(EditLocationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _warehouseService.UpdateLocationAsync(model);
            if (!result)
            {
                TempData["ErrorMessage"] = "Location not found!";
                return RedirectToAction(nameof(LocationList));
            }

            TempData["SuccessMessage"] = "Location updated successfully!";
            return RedirectToAction(nameof(LocationDetails), new { id = model.LocationId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error updating location: {ex.Message}";
            return View(model);
        }
    }

    // POST: Warehouse/DeleteLocation/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLocation(int id, int? returnToWarehouse)
    {
        try
        {
            var result = await _warehouseService.DeleteLocationAsync(id);
            if (!result)
            {
                TempData["ErrorMessage"] = "Location not found!";
                return RedirectToAction(nameof(LocationList));
            }

            TempData["SuccessMessage"] = "Location deleted successfully!";
            
            if (returnToWarehouse.HasValue)
            {
                return RedirectToAction(nameof(Details), new { id = returnToWarehouse.Value });
            }
            
            return RedirectToAction(nameof(LocationList));
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(LocationDetails), new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting location: {ex.Message}";
            return RedirectToAction(nameof(LocationDetails), new { id });
        }
    }

    // API Endpoints for AJAX calls
    [HttpGet]
    public async Task<IActionResult> GetWarehousePrefix(int warehouseId)
    {
        var warehouse = await _warehouseService.GetWarehouseForEditAsync(warehouseId);
        if (warehouse == null)
        {
            return NotFound();
        }

        return Json(new { prefix = warehouse.Prefix });
    }
}
