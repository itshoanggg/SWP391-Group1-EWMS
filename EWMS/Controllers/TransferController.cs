using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Services;
using EWMS.Models;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Admin,Warehouse,Inventory,Warehouse Manager,Inventory Staff")]
    public class TransferController : Controller
    {
        private readonly TransferService _transferService;

        public TransferController(TransferService transferService)
        {
            _transferService = transferService;
        }

        public async Task<IActionResult> Index()
        {
            var transfers = await _transferService.GetAllTransfersAsync();
            return View(transfers);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
            ViewBag.Products = await _transferService.GetProductsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransferRequest model, int ProductID, int Quantity)
        {
            // Validation: Check same warehouse
            if (model.FromWarehouseId == model.ToWarehouseId)
            {
                ModelState.AddModelError("", "Source and destination warehouses must be different.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View(model);
            }

            // Validation: Check quantity
            if (Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View(model);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.TryParse(userIdClaim, out var id) ? id : 0;

            if (userId == 0)
            {
                ModelState.AddModelError("", "User not authenticated.");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View(model);
            }

            try
            {
                await _transferService.CreateTransferAsync(model, ProductID, Quantity, userId);
                TempData["SuccessMessage"] = "Transfer request created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating transfer: {ex.Message}");
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transferService.GetProductsAsync();
                return View(model);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var transfer = await _transferService.GetTransferByIdAsync(id);
            if (transfer == null)
                return NotFound();

            return View(transfer);
        }
    }
}
