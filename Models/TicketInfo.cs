namespace ZammadDashboard.Models
{
    public class TicketInfo
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? EscalationAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CloseAt { get; set; }
        public int PriorityId { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public int StateId { get; set; }
        public string StateName { get; set; } = string.Empty;
        public string TimeRemaining { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public int? GroupId { get; set; }
        public int? OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
    }
}
