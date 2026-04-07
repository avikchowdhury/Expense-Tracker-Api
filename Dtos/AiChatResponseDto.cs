namespace ExpenseTracker.Api.Dtos
{
    public class AiChatResponseDto
    {
        public string Reply { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
        public List<string> ReferencedMetrics { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
