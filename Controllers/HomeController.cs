using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ZammadDashboard.Models;
using ZammadDashboard.Services;

namespace ZammadDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IZammadDashboardService _dashboardService;
        private readonly ZammadConfig _config;

        public HomeController(
            ILogger<HomeController> logger,
            IZammadDashboardService dashboardService,
            IOptions<ZammadConfig> config)
        {
            _logger = logger;
            _dashboardService = dashboardService;
            _config = config.Value;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Pass Zammad URL to view for clickable links
                ViewBag.ZammadUrl = _config.Url;

                // Load initial metrics for server-side rendering
                var metrics = await _dashboardService.GetDashboardMetricsAsync();
                return View(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");

                // Pass Zammad URL even on error
                ViewBag.ZammadUrl = _config.Url;

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