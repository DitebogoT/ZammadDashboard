namespace ZammadDashboard.Models
{
    public class TicketInfo
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? EscalationAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PriorityId { get; set; }
        public int StateId { get; set; }
        public string TimeRemaining { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }
}
