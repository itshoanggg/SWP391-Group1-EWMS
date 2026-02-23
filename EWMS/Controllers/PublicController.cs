using Microsoft.AspNetCore.Mvc;

namespace EWMS.Controllers
{
    public class PublicController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}