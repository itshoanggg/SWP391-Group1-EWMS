using Microsoft.AspNetCore.Mvc;

namespace EWMS.Repositories
{
    public class ITransferService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
