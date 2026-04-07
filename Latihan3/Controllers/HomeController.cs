using Latihan3.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Latihan3.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { Requestid = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
