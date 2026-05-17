using System.Text.Json;

namespace ExpenseTracker.Api.Services;

internal static class AIResponseTextExtractor
{
    public static string? ExtractResponseText(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("output_text", out var outputTextElement))
        {
            var outputText = outputTextElement.GetString();
            if (!string.IsNullOrWhiteSpace(outputText))
            {
                return outputText;
            }
        }

        if (!document.RootElement.TryGetProperty("output", out var outputElement) ||
            outputElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var outputItem in outputElement.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (contentItem.TryGetProperty("type", out var typeElement) &&
                    string.Equals(typeElement.GetString(), "output_text", StringComparison.OrdinalIgnoreCase) &&
                    contentItem.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        return null;
    }
}
