using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EWMS.Services;
using EWMS.Models;

namespace EWMS.Controllers
{
    [Authorize] // require login
    [Authorize(Roles = "Warehouse,Inventory,Admin")] // adjust role names to match DB values
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
            if (!ModelState.IsValid)
            {
                ViewBag.Warehouses = await _transferService.GetWarehousesAsync();
                ViewBag.Products = await _transfer_service.GetProductsAsync();
                return View(model);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.TryParse(userIdClaim, out var id) ? id : 0;

            await _transferService.CreateTransferAsync(model, ProductID, Quantity, userId);
            return RedirectToAction(nameof(Index));
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