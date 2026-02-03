namespace ZammadDashboard.Models
{
    public class ZammadConfig
    {
        public string Url { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RefreshIntervalSeconds { get; set; } = 60;
        public int SlaWarningThresholdMinutes { get; set; } = 60;
        public int P1PriorityId { get; set; } = 1;
        public int ClosedStateId { get; set; } = 4;

        // NEW: State IDs for on-hold/awaiting
        public int OnHoldStateId { get; set; } = 3;
        public int PendingReminderStateId { get; set; } = 6;
        public int PendingCloseStateId { get; set; } = 7;

        public int CacheExpirationSeconds { get; set; } = 30;
    }
}
