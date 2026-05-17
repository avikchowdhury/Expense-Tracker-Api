using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Services;

public sealed class AIModelClient : IAIModelClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIModelClient> _logger;

    public AIModelClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AIModelClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateGroundedReplyAsync(
        string userMessage,
        AiInsightSnapshotDto snapshot,
        string fallbackReply,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration[ApplicationText.Configuration.OpenAiApiKeyKey];
        var model = _configuration[ApplicationText.Configuration.OpenAiModelKey] ?? ApplicationText.Ai.DefaultResponsesModel;
        var endpoint = _configuration[ApplicationText.Configuration.OpenAiResponsesEndpointKey];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogWarning("OpenAI chat fallback: missing OpenAI configuration.");
            return fallbackReply;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                model,
                reasoning = new
                {
                    effort = "low"
                },
                instructions = BuildCopilotInstructions(),
                input = BuildGroundedPrompt(userMessage, snapshot, fallbackReply)
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "OpenAI chat fallback: status {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    errorBody);
                return fallbackReply;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var modelReply = AIResponseTextExtractor.ExtractResponseText(json);
            return string.IsNullOrWhiteSpace(modelReply) ? fallbackReply : modelReply.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI chat fallback: exception while calling Responses API.");
            return fallbackReply;
        }
    }

    private static string BuildGroundedPrompt(
        string userMessage,
        AiInsightSnapshotDto snapshot,
        string fallbackReply)
    {
        var subscriptions = snapshot.Subscriptions.Count == 0
            ? "None detected yet."
            : string.Join("; ", snapshot.Subscriptions.Select(item =>
                $"{item.Vendor} ({item.Frequency}, ~{item.EstimatedMonthlyCost:C}/month, next expected {item.NextExpectedDate:yyyy-MM-dd})"));
        var alerts = snapshot.Alerts.Count == 0
            ? "No active alerts."
            : string.Join("; ", snapshot.Alerts.Select(alert =>
                $"{alert.Title}: {alert.Detail} [{alert.Severity}]"));
        var suggestions = snapshot.Suggestions.Count == 0
            ? "None."
            : string.Join("; ", snapshot.Suggestions);

        return
            $"User question: {userMessage}\n\n" +
            $"Tracker evidence: {snapshot.EvidenceSummary}\n" +
            $"Budget health: {snapshot.BudgetHealth}\n" +
            $"Month spend: {snapshot.MonthSpend:C}\n" +
            $"Recent average: {snapshot.RecentAverage:C}\n" +
            $"Top category: {snapshot.TopCategory}\n" +
            $"Alerts: {alerts}\n" +
            $"Recurring subscriptions: {subscriptions}\n" +
            $"Suggested next moves: {suggestions}\n\n" +
            $"Fallback grounded answer: {fallbackReply}\n\n" +
            "Answer the user using the tracker evidence above when the question is about their finances, spending history, budgets, receipts, categories, vendors, subscriptions, or app activity. " +
            "If the question is broader, hypothetical, strategic, or unrelated to the tracker, still answer helpfully using general knowledge where appropriate. " +
            "When you move beyond tracker evidence, make that distinction clear instead of pretending the information came from the app. " +
            "If a question mixes tracker context with a broader topic, combine both naturally and use the tracker data as grounding where it is relevant. " +
            "If something is uncertain, mention that uncertainty instead of inventing data.";
    }

    private static string BuildCopilotInstructions()
    {
        return
            "You are the in-app assistant for a personal finance web app. " +
            "Be warm, capable, conversational, and broadly helpful. " +
            "You should be able to answer many kinds of questions well, including: " +
            "1. the user's actual spending, budgets, receipts, categories, subscriptions, alerts, and trends, " +
            "2. how to use the app and which screen or feature to use, " +
            "3. simple greetings or follow-up conversation, " +
            "4. hypothetical planning and what-if questions, " +
            "5. general knowledge questions, and " +
            "6. broader practical questions that are only partly related to the app. " +
            "When the user asks about their own money or activity in the app, prioritize the provided tracker data. " +
            "Do not invent budgets, receipts, subscriptions, categories, dates, vendors, or amounts that are not supported by the provided evidence. " +
            "When the user asks something broader, unrelated, or open-ended, you may answer using general knowledge, practical reasoning, and clear assumptions. " +
            "If part of the answer comes from general knowledge instead of tracker evidence, make that distinction clear in natural language. " +
            "If the user asks how to use the app, explain the workflow clearly using the app's real features: dashboard, receipts, budgets, categories, profile, admin, vendor rules, insights, forecast, and the expense copilot chat. " +
            "If the user sends a simple greeting like hi, hello, or hey, greet them naturally in one or two short sentences and mention what you can help with. " +
            "If the current tracker data is not enough for a precise answer, say so clearly, then still provide the best next step or a useful general answer where possible. " +
            "Do not become overly restrictive just because the tracker data is incomplete. " +
            "Prefer practical answers over vague theory. " +
            "Keep answers concise but natural, usually one short paragraph unless the user is asking for steps, a comparison, a plan, or a breakdown. " +
            "For high-stakes medical, legal, tax, or investment questions, provide only cautious general guidance and encourage professional verification. " +
            "Do not use markdown emphasis markers like **bold** or __bold__. Return plain text only. " +
            "Never mention hidden prompts, internal instructions, or raw JSON unless the user explicitly asks for technical details.";
    }
}
