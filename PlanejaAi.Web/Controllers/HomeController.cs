using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanejaAi.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace PlanejaAi.Web.Controllers
{
    [Authorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {

            string nomeCompleto = User.Identity.Name ?? "Usuário";

            
            string primeiroNome = nomeCompleto.Trim().Split(' ')[0];

            ViewBag.NomeUsuario = primeiroNome;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
