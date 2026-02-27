using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using EWMS.ViewModels;
using EWMS.Models;

namespace EWMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly EWMSDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(EWMSDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            // Explicit path to the cleaned login view to avoid duplicate-view confusion
            return View("~/Views/Account/Login.cshtml");
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                // Return the canonical view path to ensure UI changes are used
                return View("~/Views/Account/Login.cshtml", model);

            var user = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.UserWarehouses)
                    .ThenInclude(uw => uw.Warehouse)
                .SingleOrDefaultAsync(u => u.Username == model.Username);

            if (user == null || user.IsActive == false)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View("~/Views/Account/Login.cshtml", model);
            }

            // Verify password - support both plain text (legacy) and hashed passwords
            bool passwordValid = false;
            
            // First try: Check if it's a plain text password (legacy support)
            if (user.PasswordHash == model.Password)
            {
                passwordValid = true;
                
                // Auto-upgrade to hashed password for better security
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                await _db.SaveChangesAsync();
            }
            else
            {
                // Second try: Verify as hashed password
                var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                if (verifyResult != PasswordVerificationResult.Failed)
                {
                    passwordValid = true;
                }
            }

            if (!passwordValid)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View("~/Views/Account/Login.cshtml", model);
            }

            var warehouseName = user.UserWarehouses.FirstOrDefault()?.Warehouse?.WarehouseName ?? "No Warehouse";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.GivenName, user.FullName ?? user.Username),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? string.Empty),
                new Claim("WarehouseName", warehouseName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            // Prefer role-based landing unless user explicitly navigated to a specific non-default page
            var defaultPath = Url.Action("Index", "StockIn") ?? "/StockIn/Index";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                var normalizedReturn = returnUrl.TrimEnd('/');
                var normalizedDefault = defaultPath.TrimEnd('/');
                if (!string.Equals(normalizedReturn, normalizedDefault, StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }
            }

            // Redirect by role to each user's page
            var role = (user.Role?.RoleName ?? string.Empty).Trim().ToLowerInvariant();

            // Admin - redirect to User Management
            if (role.Contains("admin"))
                return RedirectToAction("Index", "Users");

            // Sales team
            if (role.Contains("sale"))
                return RedirectToAction("Index", "SalesOrder");

            // Purchasing/Procurement team (cover multiple variants)
            if (role.Contains("purch") || role.Contains("procure"))
                return RedirectToAction("Index", "PurchaseOrder");

            // Inventory/Warehouse team
            if (role.Contains("invent") || role.Contains("warehouse"))
                return RedirectToAction("Index", "StockIn");

            // Fallback - redirect to User Management for unrecognized roles
            return RedirectToAction("Index", "Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

 
        [HttpGet]
        public async Task<IActionResult> LogoutGet()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
