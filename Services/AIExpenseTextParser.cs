using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Services;

public sealed class AIExpenseTextParser : IAIExpenseTextParser
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AIExpenseTextParser(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<ParseTextResultDto> ParseAsync(string text, CancellationToken cancellationToken = default)
    {
        var fallback = new ParseTextResultDto
        {
            Vendor = ApplicationText.Defaults.UnknownVendor,
            Amount = 0,
            Category = ApplicationText.Defaults.GeneralCategory,
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Parsed = false,
            RawText = text
        };

        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        var apiKey = _configuration[ApplicationText.Configuration.OpenAiApiKeyKey];
        var model = _configuration[ApplicationText.Configuration.OpenAiModelKey] ?? ApplicationText.Ai.DefaultResponsesModel;
        var endpoint = _configuration[ApplicationText.Configuration.OpenAiResponsesEndpointKey];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
            return TryParseTextLocally(text);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                model,
                reasoning = new { effort = "low" },
                instructions = ApplicationText.Ai.ReceiptParserInstructions,
                input = $"Parse this expense: {text}\nToday is {DateTime.UtcNow:yyyy-MM-dd}"
            };

            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return TryParseTextLocally(text);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var rawText = AIResponseTextExtractor.ExtractResponseText(json);
            if (string.IsNullOrWhiteSpace(rawText))
                return TryParseTextLocally(text);

            var startIdx = rawText.IndexOf('{');
            var endIdx = rawText.LastIndexOf('}');
            if (startIdx < 0 || endIdx < 0)
                return TryParseTextLocally(text);

            var jsonSlice = rawText[startIdx..(endIdx + 1)];
            using var doc = JsonDocument.Parse(jsonSlice);
            var root = doc.RootElement;

            return new ParseTextResultDto
            {
                Vendor = root.TryGetProperty("vendor", out var vendor) ? vendor.GetString() ?? ApplicationText.Defaults.UnknownVendor : ApplicationText.Defaults.UnknownVendor,
                Amount = root.TryGetProperty("amount", out var amount) ? amount.GetDecimal() : 0,
                Category = root.TryGetProperty("category", out var category) ? category.GetString() ?? ApplicationText.Defaults.GeneralCategory : ApplicationText.Defaults.GeneralCategory,
                Date = root.TryGetProperty("date", out var date) ? date.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Parsed = true,
                RawText = text
            };
        }
        catch
        {
            return TryParseTextLocally(text);
        }
    }

    private static ParseTextResultDto TryParseTextLocally(string text)
    {
        var amountMatch = Regex.Match(text, @"\$?([\d,]+\.?\d*)");
        var amount = amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", ""), out var parsedAmount)
            ? parsedAmount
            : 0m;

        return new ParseTextResultDto
        {
            Vendor = ApplicationText.Defaults.UnknownVendor,
            Amount = amount,
            Category = ApplicationText.Defaults.GeneralCategory,
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Parsed = amount > 0,
            RawText = text
        };
    }
}
