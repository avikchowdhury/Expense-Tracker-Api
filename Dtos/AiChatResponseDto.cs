namespace ExpenseTracker.Api.Dtos
{
    public class AiChatResponseDto
    {
        public string Reply { get; set; } = string.Empty;
        public string EvidenceSummary { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
        public List<string> ReferencedMetrics { get; set; } = new();
        public List<AiCopilotCardDto> Cards { get; set; } = new();
        public List<AiCopilotAlertDto> Alerts { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
