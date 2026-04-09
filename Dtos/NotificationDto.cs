namespace ExpenseTracker.Api.Dtos
{
    public class NotificationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // budget | anomaly | subscription | info
        public string Severity { get; set; } = "info"; // info | warning | critical
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
