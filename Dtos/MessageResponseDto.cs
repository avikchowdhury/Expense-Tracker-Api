namespace ExpenseTracker.Api.Dtos;

public sealed class MessageResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string? DevelopmentToken { get; set; }
}
