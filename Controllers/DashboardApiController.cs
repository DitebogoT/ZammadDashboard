using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ZammadDashboard.Models;
using ZammadDashboard.Services;

namespace ZammadDashboard.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardApiController : ControllerBase
    {
        private readonly ZammadDashboardService _dashboardService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DashboardApiController> _logger;
        private const string CacheKey = "dashboard_metrics";

        public DashboardApiController(
            ZammadDashboardService dashboardService,
            IMemoryCache cache,
            ILogger<DashboardApiController> logger)
        {
            _dashboardService = dashboardService;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<DashboardMetrics>> GetMetrics()
        {
            try
            {
                // Try to get from cache first
                if (_cache.TryGetValue(CacheKey, out DashboardMetrics? cachedMetrics)
                    && cachedMetrics != null)
                {
                    _logger.LogInformation("Returning cached metrics");
                    return Ok(cachedMetrics);
                }

                // Fetch fresh data
                _logger.LogInformation("Cache miss, fetching fresh metrics");
                var metrics = await _dashboardService.GetDashboardMetricsAsync();

                // Cache for 30 seconds
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(30));

                _cache.Set(CacheKey, metrics, cacheOptions);

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard metrics");
                return StatusCode(500, new
                {
                    error = "Failed to fetch metrics",
                    message = ex.Message
                });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<DashboardMetrics>> RefreshMetrics()
        {
            try
            {
                _logger.LogInformation("Force refresh requested");

                // Clear cache
                _cache.Remove(CacheKey);

                // Fetch fresh data
                var metrics = await _dashboardService.GetDashboardMetricsAsync();

                // Cache for 30 seconds
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(30));

                _cache.Set(CacheKey, metrics, cacheOptions);

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard metrics");
                return StatusCode(500, new
                {
                    error = "Failed to refresh metrics",
                    message = ex.Message
                });
            }
        }
    }
}