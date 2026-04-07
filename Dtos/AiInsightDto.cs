namespace ExpenseTracker.Api.Dtos
{
    public class AiInsightDto
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string? MetricLabel { get; set; }
        public string? MetricValue { get; set; }
        public string? Action { get; set; }
    }
}
