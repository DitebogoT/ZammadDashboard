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
                
                // NEW: Calculate P1 on-hold/awaiting tickets
                CalculateP1OnHoldTickets(openTickets, metrics);
                
                // NEW: Calculate today's tickets and closed tickets
                await CalculateTodayTickets(metrics);
                await CalculateTodayClosedTickets(metrics);
                await CalculateTodayYesterdayTickets(metrics);

                _logger.LogInformation(
                    "Metrics calculated - Breaches: {Breaches}, At Risk: {AtRisk}, P1: {P1}, P1 On Hold: {OnHold}, 48hr+: {Hours48}, Today Closed: {Closed}",
                    metrics.SlaBreaches, metrics.SlaAtRisk, metrics.OpenP1Tickets, metrics.P1OnHoldCount, 
                    metrics.TicketsOpenMoreThan48Hours, metrics.TodayClosedCount
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
                    escalationTime = ticket.EscalationAt?.UtcDateTime;
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

                    var ticketInfo = CreateTicketInfo(ticket);

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
                ticket.CloseEscalationAt?.UtcDateTime
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
                var ticketInfo = CreateTicketInfo(ticket);
                ticketInfo.TimeRemaining = GetAgeString(ticket.CreatedAt.UtcDateTime);
                metrics.P1Tickets.Add(ticketInfo);
            }
        }

        // NEW: Calculate P1 tickets that are on hold or awaiting
        private void CalculateP1OnHoldTickets(List<Ticket> openTickets, DashboardMetrics metrics)
        {
            var onHoldStateIds = new List<int> 
            { 
                _config.OnHoldStateId, 
                _config.PendingReminderStateId,
                _config.PendingCloseStateId
            };

            var p1OnHoldTickets = openTickets
                .Where(t => t.PriorityId == _config.P1PriorityId && 
                           onHoldStateIds.Contains(t.StateId ?? 0))
                .ToList();

            metrics.P1OnHoldCount = p1OnHoldTickets.Count;

            foreach (var ticket in p1OnHoldTickets)
            {
                var ticketInfo = CreateTicketInfo(ticket);
                ticketInfo.TimeRemaining = GetAgeString(ticket.CreatedAt.UtcDateTime);
                metrics.P1OnHoldTickets.Add(ticketInfo);
            }

            _logger.LogInformation(
                "Found {Count} P1 tickets on hold/awaiting (State IDs: {States})",
                metrics.P1OnHoldCount,
                string.Join(", ", onHoldStateIds)
            );
        }

        private void Calculate48HourTickets(List<Ticket> openTickets, DashboardMetrics metrics)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-48);
            var oldTickets = openTickets.Where(t => t.CreatedAt < cutoffTime).ToList();
            metrics.TicketsOpenMoreThan48Hours = oldTickets.Count;

            foreach (var ticket in oldTickets)
            {
                var ticketInfo = CreateTicketInfo(ticket);
                ticketInfo.TimeRemaining = GetAgeString(ticket.CreatedAt.UtcDateTime);
                metrics.Tickets48HoursPlus.Add(ticketInfo);
            }
        }

        // NEW: Get all tickets created today with full details
        private async Task CalculateTodayTickets(DashboardMetrics metrics)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            try
            {
                var todayStart = today.ToString("yyyy-MM-dd");
                var todayEnd = tomorrow.ToString("yyyy-MM-dd");
                
                var todayTickets = await _ticketClient.SearchTicketAsync(
                    $"created_at:[{todayStart} TO {todayEnd}]",
                    1000
                );

                if (todayTickets != null && todayTickets.Any())
                {
                    metrics.TodayTicketCount = todayTickets.Count;
                    
                    foreach (var ticket in todayTickets.OrderByDescending(t => t.CreatedAt))
                    {
                        var ticketInfo = CreateTicketInfo(ticket);
                        ticketInfo.TimeRemaining = GetAgeString(ticket.CreatedAt.UtcDateTime);
                        metrics.TodayAllTickets.Add(ticketInfo);
                    }

                    _logger.LogInformation("Found {Count} tickets created today", metrics.TodayTicketCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching today's tickets");
            }
        }

        // NEW: Get all tickets closed today
        private async Task CalculateTodayClosedTickets(DashboardMetrics metrics)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            try
            {
                var todayStart = today.ToString("yyyy-MM-dd");
                var todayEnd = tomorrow.ToString("yyyy-MM-dd");
                
                // Search for tickets closed today
                var closedTickets = await _ticketClient.SearchTicketAsync(
                    $"close_at:[{todayStart} TO {todayEnd}]",
                    1000
                );

                if (closedTickets != null && closedTickets.Any())
                {
                    metrics.TodayClosedCount = closedTickets.Count;
                    
                    foreach (var ticket in closedTickets.OrderByDescending(t => t.CloseAt))
                    {
                        var ticketInfo = CreateTicketInfo(ticket);
                        
                        // Calculate resolution time
                        if (ticket.CloseAt.HasValue)
                        {
                            var resolutionTime = ticket.CloseAt.Value - ticket.CreatedAt;
                            ticketInfo.TimeRemaining = FormatResolutionTime(resolutionTime);
                        }
                        
                        metrics.TodayClosedTickets.Add(ticketInfo);
                    }

                    _logger.LogInformation("Found {Count} tickets closed today", metrics.TodayClosedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching today's closed tickets");
                
                // Fallback: filter from all tickets
                try
                {
                    var allTickets = await _ticketClient.GetTicketListAsync();
                    var closedToday = allTickets?
                        .Where(t => t.StateId == _config.ClosedStateId && 
                                   t.CloseAt.HasValue && 
                                   t.CloseAt.Value.Date == today)
                        .ToList();

                    if (closedToday != null && closedToday.Any())
                    {
                        metrics.TodayClosedCount = closedToday.Count;
                        
                        foreach (var ticket in closedToday.OrderByDescending(t => t.CloseAt))
                        {
                            var ticketInfo = CreateTicketInfo(ticket);
                            
                            if (ticket.CloseAt.HasValue)
                            {
                                var resolutionTime = ticket.CloseAt.Value - ticket.CreatedAt;
                                ticketInfo.TimeRemaining = FormatResolutionTime(resolutionTime);
                            }
                            
                            metrics.TodayClosedTickets.Add(ticketInfo);
                        }
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Fallback for closed tickets also failed");
                }
            }
        }

        private async Task CalculateTodayYesterdayTickets(DashboardMetrics metrics)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            try
            {
                // If we already got today's count from CalculateTodayTickets, skip
                if (metrics.TodayTicketCount == 0)
                {
                    var todayStart = today.ToString("yyyy-MM-dd");
                    var todayEnd = today.AddDays(1).ToString("yyyy-MM-dd");
                    var todayTickets = await _ticketClient.SearchTicketAsync(
                        $"created_at:[{todayStart} TO {todayEnd}]",
                        1000
                    );
                    metrics.TodayTicketCount = todayTickets?.Count ?? 0;
                }

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
            }
        }

        // Helper method to create TicketInfo with all details
        private TicketInfo CreateTicketInfo(Ticket ticket)
        {
            return new TicketInfo
            {
                Id = ticket.Id,
                Number = ticket.Number,
                Title = ticket.Title,
                EscalationAt = ticket.EscalationAt?.UtcDateTime,
                CreatedAt = ticket.CreatedAt.UtcDateTime,
                UpdatedAt = ticket.UpdatedAt.UtcDateTime,
                CloseAt = ticket.CloseAt?.UtcDateTime,
                PriorityId = ticket.PriorityId ?? 0,
                StateId = ticket.StateId ?? 0,
                CustomerId = ticket.CustomerId,
                GroupId = ticket.GroupId,
                OwnerId = ticket.OwnerId,
                // Note: Zammad.Client may not have these name fields directly
                // You might need to make separate API calls to get names
                PriorityName = GetPriorityName(ticket.PriorityId ?? 0),
                StateName = GetStateName(ticket.StateId ?? 0)
            };
        }

        private string GetPriorityName(int priorityId)
        {
            // Map priority IDs to names based on standard Zammad setup
            return priorityId switch
            {
                1 => "P1",
                2 => "P2",
                3 => "P3",
                4 => "P4",
                _ => $"Priority {priorityId}"
            };
        }

        private string GetStateName(int stateId)
        {
            // Map state IDs to names based on standard Zammad setup
            return stateId switch
            {
                1 => "Assigned",
                2 => "Awaiting",
                3 => "Cancelled",
                4 => "Resolved",
                5 => "Escalation",
                6 => "In Progress",
                7 => "Investigation",
                8 => "Notification",
                9 => "On Hold",
                10 => "Open",
                11 => "Pending Close",
                12 => "Notification",
                13 => "Resolved",
                _ => $"State {stateId}"
            };
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

        private string FormatResolutionTime(TimeSpan resolutionTime)
        {
            if (resolutionTime.TotalDays >= 1)
                return $"Resolved in {(int)resolutionTime.TotalDays}d {resolutionTime.Hours}h";
            else if (resolutionTime.TotalHours >= 1)
                return $"Resolved in {(int)resolutionTime.TotalHours}h {resolutionTime.Minutes}m";
            else
                return $"Resolved in {(int)resolutionTime.TotalMinutes}m";
        }
    }
}