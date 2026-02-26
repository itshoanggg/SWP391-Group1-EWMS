using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EWMS.Models;
using EWMS.ViewModels;

namespace EWMS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly EWMSDbContext _db;
        private readonly IPasswordHasher<User> _hasher;

        public UsersController(EWMSDbContext db, IPasswordHasher<User> hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.UserWarehouses)
                    .ThenInclude(uw => uw.Warehouse)
                .OrderBy(u => u.Username)
                .ToListAsync();

            var vm = users.Select(u => new UserListItemViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                RoleName = u.Role?.RoleName ?? string.Empty,
                IsActive = u.IsActive ?? false,
                Warehouses = u.UserWarehouses.Select(uw => uw.Warehouse?.WarehouseName ?? string.Empty).ToList()
            }).ToList();

            return View(vm);
        }

        // GET: Users/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
            ViewBag.Warehouses = await _db.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            ViewBag.Roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
            ViewBag.Warehouses = await _db.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();

            if (!ModelState.IsValid)
                return View(model);

            // check unique username
            if (await _db.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError(nameof(model.Username), "Username already exists.");
                return View(model);
            }

            var user = new User
            {
                Username = model.Username,
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                RoleId = model.RoleId,
                IsActive = model.IsActive,
            };

            // Use a secure hashed password. If no password provided, generate a temporary one and force reset elsewhere.
            var passwordToHash = string.IsNullOrWhiteSpace(model.Password)
                ? "Temp!" + Guid.NewGuid().ToString("N").Substring(0, 8)
                : model.Password;

            user.PasswordHash = _hasher.HashPassword(user, passwordToHash);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // assign warehouses (departments)
            if (model.WarehouseIds?.Any() == true)
            {
                foreach (var wid in model.WarehouseIds.Distinct())
                {
                    _db.UserWarehouses.Add(new UserWarehouse { UserId = user.UserId, WarehouseId = wid });
                }
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "User created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _db.Users
                .Include(u => u.UserWarehouses)
                .SingleOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound();

            ViewBag.Roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
            ViewBag.Warehouses = await _db.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();

            var model = new EditUserViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                IsActive = user.IsActive ?? false,
                WarehouseIds = user.UserWarehouses.Select(uw => uw.WarehouseId).ToList()
            };

            return View(model);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            ViewBag.Roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
            ViewBag.Warehouses = await _db.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();

            if (!ModelState.IsValid)
                return View(model);

            var user = await _db.Users
                .Include(u => u.UserWarehouses)
                .SingleOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null)
                return NotFound();

            // username change allowed if unique
            if (!string.Equals(user.Username, model.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (await _db.Users.AnyAsync(u => u.Username == model.Username && u.UserId != model.UserId))
                {
                    ModelState.AddModelError(nameof(model.Username), "Username already exists.");
                    return View(model);
                }
                user.Username = model.Username;
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.RoleId = model.RoleId;
            user.IsActive = model.IsActive;
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = _hasher.HashPassword(user, model.Password);
            }

            // update warehouses
            var existing = user.UserWarehouses.ToList();
            // remove unselected
            var toRemove = existing.Where(e => model.WarehouseIds == null || !model.WarehouseIds.Contains(e.WarehouseId)).ToList();
            if (toRemove.Any())
                _db.UserWarehouses.RemoveRange(toRemove);

            // add new
            var existingIds = existing.Select(e => e.WarehouseId).ToHashSet();
            if (model.WarehouseIds != null)
            {
                var toAdd = model.WarehouseIds.Distinct().Where(wid => !existingIds.Contains(wid))
                    .Select(wid => new UserWarehouse { UserId = user.UserId, WarehouseId = wid }).ToList();
                if (toAdd.Any())
                    _db.UserWarehouses.AddRange(toAdd);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Users/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users
                .Include(u => u.UserWarehouses)
                .Include(u => u.Role)
                .SingleOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound();

            // Prevent deleting yourself
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == user.UserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent deleting the last admin
            var adminCount = await _db.Users
                .Include(u => u.Role)
                .CountAsync(u => u.Role != null && u.Role.RoleName == "Admin");

            if (user.Role != null && user.Role.RoleName == "Admin" && adminCount <= 1)
            {
                TempData["Error"] = "Cannot delete the last Admin account.";
                return RedirectToAction(nameof(Index));
            }

            // Remove dependent relations explicitly to avoid FK constraint issues
            if (user.UserWarehouses.Any())
            {
                _db.UserWarehouses.RemoveRange(user.UserWarehouses);
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/AssignRoles/5
        public async Task<IActionResult> AssignRoles(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            ViewBag.Roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
            var model = new AssignRoleViewModel { UserId = user.UserId, Username = user.Username, RoleId = user.RoleId };
            return View(model);
        }

        // POST: Users/AssignRoles
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRoles(AssignRoleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
                return View(model);
            }

            var user = await _db.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            user.RoleId = model.RoleId;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Role assigned.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/AssignDepartments/5
        public async Task<IActionResult> AssignDepartments(int id)
        {
            var user = await _db.Users
                .Include(u => u.UserWarehouses)
                .SingleOrDefaultAsync(u => u.UserId == id);

            if (user == null) return NotFound();

            var warehouses = await _db.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            var model = new AssignDepartmentsViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                SelectedWarehouseIds = user.UserWarehouses.Select(uw => uw.WarehouseId).ToList(),
                AvailableWarehouses = warehouses.Select(w => new WarehouseSelectItem { WarehouseId = w.WarehouseId, WarehouseName = w.WarehouseName }).ToList()
            };

            return View(model);
        }

        // POST: Users/AssignDepartments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDepartments(AssignDepartmentsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableWarehouses = (await _db.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync())
                    .Select(w => new WarehouseSelectItem { WarehouseId = w.WarehouseId, WarehouseName = w.WarehouseName }).ToList();
                return View(model);
            }

            var user = await _db.Users.Include(u => u.UserWarehouses).SingleOrDefaultAsync(u => u.UserId == model.UserId);
            if (user == null) return NotFound();

            // remove all existing
            _db.UserWarehouses.RemoveRange(user.UserWarehouses);

            // add selected
            if (model.SelectedWarehouseIds?.Any() == true)
            {
                foreach (var wid in model.SelectedWarehouseIds.Distinct())
                {
                    _db.UserWarehouses.Add(new UserWarehouse { UserId = user.UserId, WarehouseId = wid });
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Departments updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
