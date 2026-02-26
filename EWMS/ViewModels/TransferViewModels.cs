using Microsoft.AspNetCore.Mvc;

namespace EWMS.ViewModels
{
    public class TransferViewModels : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
