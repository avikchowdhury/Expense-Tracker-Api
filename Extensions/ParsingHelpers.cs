namespace ExpenseTracker.Api.Extensions
{
    public class ReceiptParsedResult
    {
        public decimal? TotalAmount { get; set; }
        public string? Vendor { get; set; }
        public string? Category { get; set; }
    }

    public static class ParsingHelpers
    {
        public static ReceiptParsedResult ParseParsedReceipt(string json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new ReceiptParsedResult
                {
                    Vendor = root.TryGetProperty("Vendor", out var vendor) ? vendor.GetString() : null,
                    Category = root.TryGetProperty("Category", out var category) ? category.GetString() : null,
                    TotalAmount = root.TryGetProperty("TotalAmount", out var total) && total.TryGetDecimal(out var value)
                        ? value
                        : (decimal?)null
                };
            }
            catch
            {
                return new ReceiptParsedResult();
            }
        }
    }
}
