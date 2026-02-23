using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;

namespace EWMS.Controllers
{
    [Authorize]
    public class TransferController : Controller
    {
        private readonly EWMSDbContext _context;

        public TransferController(EWMSDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var transfers = await _context.TransferRequests
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RequestedByNavigation)
                .OrderByDescending(t => t.RequestedDate)
                .ToListAsync();

            return View(transfers);
        }

        public IActionResult Create()
        {
            ViewBag.Warehouses = _context.Warehouses.ToList();
            ViewBag.Products = _context.Products.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            int FromWarehouseId,
            int ToWarehouseId,
            int ProductID,
            int Quantity,
            string TransferType,
            string? Reason)
        {
            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim == null) return Challenge();

            var userId = int.Parse(userIdClaim.Value);

            var transfer = new TransferRequest
            {
                FromWarehouseId = FromWarehouseId,
                ToWarehouseId = ToWarehouseId,
                TransferType = TransferType,
                RequestedBy = userId,
                RequestedDate = DateTime.Now,
                Status = "Pending",
                Reason = Reason
            };

            _context.TransferRequests.Add(transfer);
            await _context.SaveChangesAsync();

            var detail = new TransferDetail
            {
                TransferId = transfer.TransferId,
                ProductId = ProductID,
                Quantity = Quantity
            };

            _context.TransferDetails.Add(detail);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ==========================================
        // HÀM APPROVE ĐÃ ĐƯỢC TÍCH HỢP VÀ TỐI ƯU
        // ==========================================
        [Authorize(Roles = "Manager")]
        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var transfer = await _context.TransferRequests
                    .Include(t => t.TransferDetails)
                    .FirstOrDefaultAsync(t => t.TransferId == id);

                if (transfer == null || transfer.Status != "Pending")
                    return NotFound();

                var userId = int.Parse(User.FindFirst("UserID").Value);

                // 1. Cập nhật trạng thái Transfer
                transfer.Status = "Approved";
                transfer.ApprovedBy = userId;
                transfer.ApprovedDate = DateTime.Now;

                // 2. Lấy Location mặc định của kho đi và kho đến
                var fromLocation = await _context.Locations
                    .FirstOrDefaultAsync(l => l.WarehouseId == transfer.FromWarehouseId);
                var toLocation = await _context.Locations
                    .FirstOrDefaultAsync(l => l.WarehouseId == transfer.ToWarehouseId);

                if (fromLocation == null || toLocation == null)
                    throw new Exception("Kho nguồn hoặc kho đích chưa cấu hình Location.");

                // 3. Tạo các phiếu Receipt
                var stockOutReceipt = new StockOutReceipt
                {
                    WarehouseId = transfer.FromWarehouseId,
                    IssuedBy = userId,
                    IssuedDate = DateTime.Now,
                    TransferId = transfer.TransferId,
                    CreatedAt = DateTime.Now
                };
                _context.StockOutReceipts.Add(stockOutReceipt);

                var stockInReceipt = new StockInReceipt
                {
                    WarehouseId = transfer.ToWarehouseId,
                    ReceivedBy = userId,
                    ReceivedDate = DateTime.Now,
                    TransferId = transfer.TransferId,
                    CreatedAt = DateTime.Now
                };
                _context.StockInReceipts.Add(stockInReceipt);

                await _context.SaveChangesAsync();

                foreach (var detail in transfer.TransferDetails)
                {
                    // --- TRỪ KHO NGUỒN (Tìm theo LocationId thay vì WarehouseId) ---
                    var sourceStock = await _context.Inventories
                        .FirstOrDefaultAsync(x => x.LocationId == fromLocation.LocationId && x.ProductId == detail.ProductId);

                    if (sourceStock == null || (sourceStock.Quantity ?? 0) < detail.Quantity)
                        throw new Exception($"Sản phẩm ID {detail.ProductId} không đủ tồn kho tại vị trí {fromLocation.LocationId}.");

                    sourceStock.Quantity -= detail.Quantity;
                    sourceStock.LastUpdated = DateTime.Now;

                    // --- CỘNG KHO ĐÍCH (Tìm theo LocationId) ---
                    var destStock = await _context.Inventories
                        .FirstOrDefaultAsync(x => x.LocationId == toLocation.LocationId && x.ProductId == detail.ProductId);

                    if (destStock == null)
                    {
                        _context.Inventories.Add(new Inventory
                        {
                            ProductId = detail.ProductId,
                            LocationId = toLocation.LocationId,
                            Quantity = detail.Quantity,
                            LastUpdated = DateTime.Now
                        });
                    }
                    else
                    {
                        destStock.Quantity = (destStock.Quantity ?? 0) + detail.Quantity;
                        destStock.LastUpdated = DateTime.Now;
                    }

                    // --- THÊM CHI TIẾT PHIẾU ---
                    _context.StockOutDetails.Add(new StockOutDetail
                    {
                        StockOutId = stockOutReceipt.StockOutId,
                        ProductId = detail.ProductId,
                        LocationId = fromLocation.LocationId,
                        Quantity = detail.Quantity,
                        UnitPrice = 0
                    });

                    _context.StockInDetails.Add(new StockInDetail
                    {
                        StockInId = stockInReceipt.StockInId,
                        ProductId = detail.ProductId,
                        LocationId = toLocation.LocationId,
                        Quantity = detail.Quantity,
                        UnitPrice = 0
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["Success"] = "Chuyển kho thành công!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}