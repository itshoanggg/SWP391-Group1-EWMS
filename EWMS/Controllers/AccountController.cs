using EWMS.Services;
using EWMS.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EWMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        // GET
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userService.ValidateUserAsync(model.Email, model.Password);

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password";
                return View(model);
            }

            var warehouseId = user.UserWarehouses.FirstOrDefault()?.WarehouseId ?? 0;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Staff"),
                new Claim("WarehouseId", warehouseId.ToString())
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "SalesOrder");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Public");
        }
    }
}