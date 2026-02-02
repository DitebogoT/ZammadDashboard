using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ZammadDashboard.Models;
using ZammadDashboard.Services;

namespace ZammadDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IZammadDashboardService _dashboardService;

        public HomeController(
            ILogger<HomeController> logger,
            IZammadDashboardService dashboardService)
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

                // Pass Zammad URL to view for ticket links
                var config = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Models.ZammadConfig>>();
                ViewBag.ZammadUrl = config.Value.Url;

                return View(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");

                // Return view with empty metrics
                var config = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Models.ZammadConfig>>();
                ViewBag.ZammadUrl = config.Value.Url;

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