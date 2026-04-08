namespace ExpenseTracker.Api.Dtos
{
    public class AiCopilotCardDto
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Tone { get; set; } = "default";
    }
}
