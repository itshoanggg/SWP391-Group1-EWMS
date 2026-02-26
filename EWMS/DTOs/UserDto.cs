using Microsoft.AspNetCore.Mvc;

namespace EWMS.DTOs
{
    public class UserDTO : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
