namespace ZammadDashboard.Models
{
    public class DashboardMetrics
    {
        public int SlaBreaches { get; set; }
        public int SlaAtRisk { get; set; }
        public int OpenP1Tickets { get; set; }
        public int TicketsOpenMoreThan48Hours { get; set; }
        public int TodayTicketCount { get; set; }
        public int YesterdayTicketCount { get; set; }

        // NEW: Additional metrics
        public int P1OnHoldCount { get; set; }
        public int TodayClosedCount { get; set; }

        public DateTime LastUpdated { get; set; }
        public List<TicketInfo> SlaBreachTickets { get; set; } = new();
        public List<TicketInfo> SlaAtRiskTickets { get; set; } = new();
        public List<TicketInfo> P1Tickets { get; set; } = new();
        public List<TicketInfo> Tickets48HoursPlus { get; set; } = new();

        // NEW: Additional ticket lists
        public List<TicketInfo> P1OnHoldTickets { get; set; } = new();
        public List<TicketInfo> TodayAllTickets { get; set; } = new();
        public List<TicketInfo> TodayClosedTickets { get; set; } = new();

        public int TicketChange => TodayTicketCount - YesterdayTicketCount;
        public double ChangePercent => YesterdayTicketCount > 0
            ? (TicketChange / (double)YesterdayTicketCount) * 100
            : 0;
    }
}
