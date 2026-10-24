using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IDBInitializer _seedDatabase;

        public HomeController(ILogger<HomeController> logger, IDBInitializer seedDatabase)
        {
            _logger = logger;
            _seedDatabase = seedDatabase;
        }

        // Screen 01 - Welcome (first page)
        public IActionResult Index()
        {
            // People who are already logged in go straight to their own home page
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole(WebConstants.RoleAdmin) || User.IsInRole(WebConstants.RoleOfficer))
                    return RedirectToAction("Dashboard", "Admin");
                if (User.IsInRole(WebConstants.RoleUser))
                    return RedirectToAction("Index", "MyPto");
            }
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Admin only (like the "Database" link in the CUT project): re-runs table creation and seeding
        [Authorize(Roles = WebConstants.RoleAdmin)]
        public async Task<IActionResult> SeedDatabase()
        {
            await _seedDatabase.InitializeAsync();
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
