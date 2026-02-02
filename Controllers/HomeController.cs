using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZammadDashboard.Models;
using ZammadDashboard.Services;


namespace ZammadDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ZammadDashboardService _dashboardService;

        public HomeController(
            ILogger<HomeController> logger,
            ZammadDashboardService dashboardService)
        {
            _logger = logger;
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Load initial metrics for server-side rendering
                var metrics = await _dashboardService.GetDashboardMetricsAsync();
                return View(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                // Return view with empty metrics
                return View(new DashboardMetrics
                {
                    LastUpdated = DateTime.Now
                });
            }
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
