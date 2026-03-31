using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ExpenseTracker.Api.Services
{
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file)
        {

            // Use correct config section and keys
            var aiEndpoint = _configuration["AzureAI:Endpoint"];
            var aiKey = _configuration["AzureAI:Key"];
            if (string.IsNullOrWhiteSpace(aiEndpoint) || !Uri.IsWellFormedUriString(aiEndpoint, UriKind.Absolute))
            {
                throw new InvalidOperationException("AzureAI:Endpoint must be set to a valid absolute URI in appsettings.json");
            }
            if (string.IsNullOrWhiteSpace(aiKey))
            {
                throw new InvalidOperationException("AzureAI:Key must be set in appsettings.json");
            }

            using var content = new MultipartFormDataContent();
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(fileContent, "file", file.FileName);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", aiKey);

            var response = await _httpClient.PostAsync(aiEndpoint, content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            // Map the AI response to ReceiptParseResult (adjust as needed)
            var result = JsonSerializer.Deserialize<ReceiptParseResult>(json);
            return result;
        }
    }
}
