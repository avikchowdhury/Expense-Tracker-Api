namespace ExpenseTracker.Api.Dtos
{
    public class AiCopilotAlertDto
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
    }
}
