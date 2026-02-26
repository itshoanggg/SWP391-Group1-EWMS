using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EWMS.ViewModels;
using EWMS.Models;

namespace EWMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly EWMSDbContext _db;

        public AccountController(EWMSDbContext db)
        {
            _db = db;
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
                .SingleOrDefaultAsync(u => u.Username == model.Username && u.PasswordHash == model.Password);

            if (user == null || user.IsActive == false)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View("~/Views/Account/Login.cshtml", model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? string.Empty)
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

            // Sales team
            if (role.Contains("sale"))
                return RedirectToAction("Index", "SalesOrder");

            // Purchasing/Procurement team (cover multiple variants)
            if (role.Contains("purch") || role.Contains("procure"))
                return RedirectToAction("Index", "PurchaseOrder");

            // Inventory/Warehouse team
            if (role.Contains("invent") || role.Contains("warehouse"))
                return RedirectToAction("Index", "StockIn");

            // Fallback
            return RedirectToAction("Index", "StockIn");
        }

        // Keep POST logout (recommended). Works with the logout form in _Layout.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // Optional: support GET-based logout for convenience (useful if layout still contains anchor links).
        // Note: GET sign-out is less secure (CSRF risk) — keep POST as primary.
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
