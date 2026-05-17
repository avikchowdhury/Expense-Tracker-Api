namespace ExpenseTracker.Api.Dtos;

public sealed class ReceiptParseResult
{
    public string Vendor { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
}
