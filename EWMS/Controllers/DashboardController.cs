using Microsoft.AspNetCore.Mvc;

namespace EWMS.Controllers
{
    public class DashboardController : Controller
    {
        // URL TEST – KHÔNG CHECK LOGIN
        public IActionResult Test()
        {
            ViewBag.Role = "Admin"; // fake role để test UI
            return View("Index");
        }

        // URL CHÍNH – SAU NÀY DÙNG THẬT
        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("Role");

            if (role == null)
                return RedirectToAction("Login", "Auth");

            ViewBag.Role = role;
            return View();
        }
    }
}
