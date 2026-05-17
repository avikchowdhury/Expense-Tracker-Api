using System.Net.Http.Headers;
using System.Text.Json;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Services;

public sealed class AIReceiptVisionParser : IAIReceiptVisionParser
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AIReceiptVisionParser(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<ReceiptParseResult> ParseAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var fileBytes = ms.ToArray();

        var aiEndpoint = _configuration[ApplicationText.Configuration.AzureAiEndpointKey];
        var aiKey = _configuration[ApplicationText.Configuration.AzureAiKeyKey];
        if (string.IsNullOrWhiteSpace(aiEndpoint) || !Uri.IsWellFormedUriString(aiEndpoint, UriKind.Absolute) || string.IsNullOrWhiteSpace(aiKey))
        {
            return BuildFallbackReceiptParse(file.FileName, fileBytes);
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(fileContent, "file", file.FileName);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", aiKey);

            var response = await _httpClient.PostAsync(aiEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ReceiptParseResult>(json);
            return result ?? BuildFallbackReceiptParse(file.FileName, fileBytes);
        }
        catch
        {
            return BuildFallbackReceiptParse(file.FileName, fileBytes);
        }
    }

    private static ReceiptParseResult BuildFallbackReceiptParse(string fileName, byte[]? fileBytes = null)
    {
        var fallback = ReceiptFallbackHelper.Parse(fileName, fileBytes);

        return new ReceiptParseResult
        {
            Vendor = fallback.Vendor,
            Amount = fallback.Amount,
            Category = fallback.Category,
            Date = fallback.Date,
            RawText = fallback.RawText
        };
    }
}
