using Microsoft.Extensions.Options;
using Zammad.Client;
using Zammad.Client.Resources;
using ZammadDashboard.Models;

namespace ZammadDashboard.Services
{
    public interface IZammadDashboardService
    {
        Task<DashboardMetrics> GetDashboardMetricsAsync();
    }

    public class ZammadDashboardService : IZammadDashboardService
    {
        private readonly ZammadAccount _account;
        private readonly TicketClient _ticketClient;
        private readonly ZammadConfig _config;
        private readonly ILogger<ZammadDashboardService> _logger;

        public ZammadDashboardService(
            IOptions<ZammadConfig> config,
            ILogger<ZammadDashboardService> logger)
        {
            _config = config.Value;
            _logger = logger;

            try
            {
                _account = ZammadAccount.CreateBasicAccount(
                    _config.Url,
                    _config.Username,
                    _config.Password
                );
                _ticketClient = _account.CreateTicketClient();
                _logger.LogInformation("Successfully connected to Zammad at {Url}", _config.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Zammad connection");
                throw;
            }
        }

        public async Task<DashboardMetrics> GetDashboardMetricsAsync()
        {
            var metrics = new DashboardMetrics
            {
                LastUpdated = DateTime.Now
            };

            try
            {
                _logger.LogInformation("Fetching dashboard metrics...");

                // Get all open tickets
                var openTickets = await GetAllOpenTicketsAsync();
                _logger.LogInformation("Found {Count} open tickets", openTickets.Count);

                // Calculate all metrics
                CalculateSlaMetrics(openTickets, metrics);
                CalculateP1Tickets(openTickets, metrics);
                Calculate48HourTickets(openTickets, metrics);
                await CalculateTodayYesterdayTickets(metrics);

                _logger.LogInformation(
                    "Metrics calculated - Breaches: {Breaches}, At Risk: {AtRisk}, P1: {P1}, 48hr+: {Hours48}",
                    metrics.SlaBreaches, metrics.SlaAtRisk, metrics.OpenP1Tickets, metrics.TicketsOpenMoreThan48Hours
                );

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard metrics");
                throw;
            }
        }

        private async Task<List<Ticket>> GetAllOpenTicketsAsync()
        {
            try
            {
                // Try to search for open tickets first (more efficient)
                var openTickets = await _ticketClient.SearchTicketAsync(
                    "state:new OR state:open OR state:pending",
                    1000
                );

                if (openTickets != null && openTickets.Any())
                {
                    return openTickets.ToList();
                }

                // Fallback: get all tickets and filter
                _logger.LogWarning("Search returned no results, falling back to GetTicketListAsync");
                var allTickets = await _ticketClient.GetTicketListAsync();
                return allTickets?.Where(t => t.StateId != _config.ClosedStateId).ToList()
                    ?? new List<Ticket>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching open tickets");

                // Last resort fallback
                try
                {
                    var allTickets = await _ticketClient.GetTicketListAsync();
                    return allTickets?.Where(t => t.StateId != _config.ClosedStateId).ToList()
                        ?? new List<Ticket>();
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Fallback also failed");
                    return new List<Ticket>();
                }
            }
        }

        private void CalculateSlaMetrics(List<Ticket> openTickets, DashboardMetrics metrics)
        {
            var now = DateTime.UtcNow;

            foreach (var ticket in openTickets)
            {
                DateTime? escalationTime = null;

                // Check for overall escalation time
                if (ticket.EscalationAt.HasValue)
                {
                    escalationTime = ticket.EscalationAt.Value.UtcDateTime;
                }
                // Check individual SLA fields
                else if (ticket.FirstResponseEscalationAt.HasValue ||
                         ticket.UpdateEscalationAt.HasValue ||
                         ticket.CloseEscalationAt.HasValue)
                {
                    escalationTime = GetEarliestEscalation(ticket);
                }

                if (escalationTime.HasValue)
                {
                    var timeUntilEscalation = escalationTime.Value - now;

                    var ticketInfo = new TicketInfo
                    {
                        Id = ticket.Id,
                        Number = ticket.Number,
                        Title = ticket.Title,
                        EscalationAt = escalationTime,
                        CreatedAt = ticket.CreatedAt.UtcDateTime,
                        PriorityId = ticket.PriorityId ?? 0,
                        StateId = ticket.StateId ?? 0,
                    };

                    // SLA already breached
                    if (timeUntilEscalation.TotalMinutes < 0)
                    {
                        metrics.SlaBreaches++;
                        var overdue = Math.Abs((int)timeUntilEscalation.TotalMinutes);
                        ticketInfo.TimeRemaining = overdue > 60
                            ? $"{overdue / 60}h {overdue % 60}m overdue"
                            : $"{overdue}m overdue";
                        metrics.SlaBreachTickets.Add(ticketInfo);
                    }
                    // SLA at risk
                    else if (timeUntilEscalation.TotalMinutes <= _config.SlaWarningThresholdMinutes)
                    {
                        metrics.SlaAtRisk++;
                        var remaining = (int)timeUntilEscalation.TotalMinutes;
                        ticketInfo.TimeRemaining = remaining > 60
                            ? $"{remaining / 60}h {remaining % 60}m remaining"
                            : $"{remaining}m remaining";
                        metrics.SlaAtRiskTickets.Add(ticketInfo);
                    }
                }
            }
        }

        private DateTime? GetEarliestEscalation(Ticket ticket)
        {
            var escalations = new List<DateTime?>
            {
                ticket.FirstResponseEscalationAt?.UtcDateTime,
                ticket.UpdateEscalationAt?.UtcDateTime,
                ticket.CloseEscalationAt ?.UtcDateTime
            };

            return escalations
                .Where(e => e.HasValue)
                .OrderBy(e => e!.Value)
                .FirstOrDefault();
        }

        private void CalculateP1Tickets(List<Ticket> openTickets, DashboardMetrics metrics)
        {
            var p1Tickets = openTickets.Where(t => t.PriorityId == _config.P1PriorityId).ToList();
            metrics.OpenP1Tickets = p1Tickets.Count;

            foreach (var ticket in p1Tickets)
            {
                var ticketInfo = new TicketInfo
                {
                    Id = ticket.Id,
                    Number = ticket.Number,
                    Title = ticket.Title,
                    EscalationAt = ticket.EscalationAt?.UtcDateTime,
                    CreatedAt = ticket.CreatedAt.UtcDateTime,
                    PriorityId = ticket.PriorityId ?? 0,
                    StateId = ticket.StateId ?? 0,
                    TimeRemaining = GetAgeString(ticket.CreatedAt.UtcDateTime)
                };
                metrics.P1Tickets.Add(ticketInfo);
            }
        }

        private void Calculate48HourTickets(List<Ticket> openTickets, DashboardMetrics metrics)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-48);
            var oldTickets = openTickets.Where(t => t.CreatedAt < cutoffTime).ToList();
            metrics.TicketsOpenMoreThan48Hours = oldTickets.Count;

            foreach (var ticket in oldTickets)
            {
                var ticketInfo = new TicketInfo
                {
                    Id = ticket.Id,
                    Number = ticket.Number,
                    Title = ticket.Title,
                    EscalationAt = ticket.EscalationAt?.UtcDateTime,
                    CreatedAt = ticket.CreatedAt.UtcDateTime,
                    PriorityId = ticket.PriorityId ?? 0,
                    StateId = ticket.StateId ?? 0,
                    TimeRemaining = GetAgeString(ticket.CreatedAt.UtcDateTime)
                };
                metrics.Tickets48HoursPlus.Add(ticketInfo);
            }
        }

        private async Task CalculateTodayYesterdayTickets(DashboardMetrics metrics)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            try
            {
                // Search for today's tickets
                var todayStart = today.ToString("yyyy-MM-dd");
                var todayEnd = today.AddDays(1).ToString("yyyy-MM-dd");
                var todayTickets = await _ticketClient.SearchTicketAsync(
                    $"created_at:[{todayStart} TO {todayEnd}]",
                    1000
                );
                metrics.TodayTicketCount = todayTickets?.Count ?? 0;

                // Search for yesterday's tickets
                var yesterdayStart = yesterday.ToString("yyyy-MM-dd");
                var yesterdayEnd = today.ToString("yyyy-MM-dd");
                var yesterdayTickets = await _ticketClient.SearchTicketAsync(
                    $"created_at:[{yesterdayStart} TO {yesterdayEnd}]",
                    1000
                );
                metrics.YesterdayTicketCount = yesterdayTickets?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating daily tickets");

                // Fallback: count from all tickets
                try
                {
                    var allTickets = await _ticketClient.GetTicketListAsync();
                    metrics.TodayTicketCount = allTickets?.Count(t => t.CreatedAt.Date == today) ?? 0;
                    metrics.YesterdayTicketCount = allTickets?.Count(t => t.CreatedAt.Date == yesterday) ?? 0;
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Fallback calculation also failed");
                }
            }
        }

        private string GetAgeString(DateTime createdAt)
        {
            var age = DateTime.UtcNow - createdAt;

            if (age.TotalDays >= 1)
                return $"{(int)age.TotalDays}d {age.Hours}h";
            else if (age.TotalHours >= 1)
                return $"{(int)age.TotalHours}h {age.Minutes}m";
            else
                return $"{(int)age.TotalMinutes}m";
        }
    }
}
