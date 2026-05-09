using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Api.Services
{
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBudgetHealthService _budgetHealthService;
        private readonly IBudgetAdvisorService _budgetAdvisorService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AIService> _logger;

        public AIService(
            HttpClient httpClient,
            IConfiguration configuration,
            IUnitOfWork unitOfWork,
            IBudgetHealthService budgetHealthService,
            IBudgetAdvisorService budgetAdvisorService,
            IMemoryCache cache,
            ILogger<AIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _budgetHealthService = budgetHealthService;
            _budgetAdvisorService = budgetAdvisorService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            var aiEndpoint = _configuration["AzureAI:Endpoint"];
            var aiKey = _configuration["AzureAI:Key"];
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

                var response = await _httpClient.PostAsync(aiEndpoint, content);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ReceiptParseResult>(json);
                return result ?? BuildFallbackReceiptParse(file.FileName, fileBytes);
            }
            catch
            {
                return BuildFallbackReceiptParse(file.FileName, fileBytes);
            }
        }

        public async Task<AiInsightSnapshotDto> GetInsightsAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("insights", userId);
            if (_cache.TryGetValue(cacheKey, out AiInsightSnapshotDto? cachedSnapshot) && cachedSnapshot != null)
            {
                return cachedSnapshot;
            }

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var recentWindowStart = now.AddMonths(-3);

            var receipts = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();

            var budgets = await _unitOfWork.Budgets.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastReset)
                .ToListAsync();

            var monthReceipts = receipts.Where(x => x.UploadedAt >= monthStart).ToList();
            var recentReceipts = receipts.Where(x => x.UploadedAt >= recentWindowStart).ToList();
            var monthSpend = monthReceipts.Sum(x => x.TotalAmount);

            var groupedRecentMonths = recentReceipts
                .GroupBy(x => new { x.UploadedAt.Year, x.UploadedAt.Month })
                .Select(group => group.Sum(item => item.TotalAmount))
                .ToList();

            var recentAverage = groupedRecentMonths.Count > 0 ? groupedRecentMonths.Average() : 0m;

            var topCategoryGroup = monthReceipts
                .Where(x => !string.IsNullOrWhiteSpace(x.Category))
                .GroupBy(x => x.Category!)
                .Select(group => new { Category = group.Key, Total = group.Sum(item => item.TotalAmount) })
                .OrderByDescending(group => group.Total)
                .FirstOrDefault();

            var topCategory = topCategoryGroup?.Category ?? "N/A";
            var budgetSnapshot = await _budgetHealthService.GetBudgetHealthAsync(userId, monthStart, monthStart.AddMonths(1));
            var budgetAdvisor = await _budgetAdvisorService.GetBudgetAdvisorAsync(userId, now);
            var subscriptions = DetectSubscriptions(receipts);
            var ratio = budgetSnapshot.Budget > 0 ? monthSpend / budgetSnapshot.Budget : 0m;
            var budgetHealth = budgetSnapshot.BudgetCount == 0
                ? "No active budget"
                : ratio >= 1m
                    ? "Over budget"
                    : ratio >= 0.8m
                        ? "Approaching budget limit"
                        : "Healthy budget pace";

            var anomalies = new List<string>();
            var suggestions = new List<string>();
            var insights = new List<AiInsightDto>();
            var alerts = BuildAlerts(
                budgetAdvisor,
                subscriptions,
                monthReceipts.Count(receipt => string.IsNullOrWhiteSpace(receipt.Category) || receipt.Category.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)),
                receipts);

            if (budgetSnapshot.BudgetCount > 0)
            {
                insights.Add(new AiInsightDto
                {
                    Title = "Budget pulse",
                    Summary = $"You have spent {monthSpend:C} against a combined {budgetSnapshot.Budget:C} budget this month.",
                    Severity = ratio >= 1m ? "critical" : ratio >= 0.8m ? "warning" : "positive",
                    MetricLabel = "Budget used",
                    MetricValue = $"{Math.Round(ratio * 100, 0)}%",
                    Action = ratio >= 0.8m ? "Review your highest categories before the next upload batch." : "Keep your current pace and monitor weekly trends.",
                    ActionLabel = ratio >= 0.8m ? "Open forecast" : "Review forecast",
                    ActionRoute = "/forecast"
                });
            }
            else
            {
                insights.Add(new AiInsightDto
                {
                    Title = "Budget setup missing",
                    Summary = "Add a monthly budget to unlock more precise overspend coaching.",
                    Severity = "info",
                    Action = "Create a General budget first so the assistant can flag risk earlier.",
                    ActionLabel = "Create budget",
                    ActionRoute = "/budgets"
                });
            }

            insights.Add(new AiInsightDto
            {
                Title = "Category focus",
                Summary = topCategory == "N/A"
                    ? "No dominant category yet because recent receipts are limited."
                    : $"{topCategory} is your leading category in the current month.",
                Severity = topCategory == "N/A" ? "info" : "positive",
                MetricLabel = "Top category",
                MetricValue = topCategory,
                Action = topCategory == "N/A" ? "Upload more receipts for stronger categorization trends." : "Compare this category against your budget or last month to spot drift.",
                ActionLabel = topCategory == "N/A" ? "Add receipts" : "Try what-if forecast",
                ActionRoute = topCategory == "N/A" ? "/receipts" : "/forecast"
            });

            var latestReceipt = receipts.FirstOrDefault();
            insights.Add(new AiInsightDto
            {
                Title = "Receipt activity",
                Summary = latestReceipt == null
                    ? "No receipts have been uploaded yet."
                    : $"Your latest receipt is from {latestReceipt.Vendor ?? "an unknown vendor"} for {latestReceipt.TotalAmount:C}.",
                Severity = latestReceipt == null ? "info" : "positive",
                MetricLabel = "Recent uploads",
                MetricValue = receipts.Take(5).Count().ToString(),
                Action = latestReceipt == null ? "Upload a receipt to start generating AI guidance." : "Use the receipts page to correct categories before the next insight refresh.",
                ActionLabel = latestReceipt == null ? "Add receipt" : "Open cleanup",
                ActionRoute = latestReceipt == null ? "/receipts" : "/receipts"
            });

            if (subscriptions.Count > 0)
            {
                var leadingSubscription = subscriptions[0];
                insights.Add(new AiInsightDto
                {
                    Title = "Recurring spend watch",
                    Summary = $"{subscriptions.Count} subscription-like merchants stand out. {leadingSubscription.Vendor} is the strongest recurring spend signal.",
                    Severity = "positive",
                    MetricLabel = "Recurring monthly",
                    MetricValue = $"{subscriptions.Sum(item => item.EstimatedMonthlyCost):C0}",
                    Action = "Review recurring charges before the next billing date and decide which ones still deserve budget room.",
                    ActionLabel = "Review subscriptions",
                    ActionRoute = "/insights?tab=subscriptions"
                });
            }

            var highestReceipt = monthReceipts.OrderByDescending(x => x.TotalAmount).FirstOrDefault();
            if (highestReceipt != null && recentAverage > 0 && highestReceipt.TotalAmount > recentAverage * 0.5m)
            {
                anomalies.Add($"A higher-value receipt from {highestReceipt.Vendor ?? "an unknown vendor"} stands out this month.");
            }

            if (ratio >= 1m)
            {
                anomalies.Add("This month's spending has already crossed your budget.");
                suggestions.Add("Pause non-essential purchases until you reset the budget.");
                suggestions.Add($"Audit receipts in {topCategory} to find the fastest savings.");
            }
            else if (ratio >= 0.8m)
            {
                anomalies.Add("Current spending is nearing the monthly ceiling.");
                suggestions.Add("Review this week's uploads before approving more discretionary spending.");
                suggestions.Add("Tighten the highest category for the rest of the month.");
            }
            else
            {
                suggestions.Add("Keep uploading receipts consistently so the assistant can detect patterns earlier.");
                suggestions.Add(topCategory == "N/A"
                    ? "Create categories or budgets to unlock stronger coaching."
                    : $"Compare {topCategory} spending against your next budget review.");
            }

            if (latestReceipt != null)
            {
                suggestions.Add($"Check whether {latestReceipt.Vendor ?? "the latest vendor"} belongs in a recurring spend category.");
            }

            var snapshot = new AiInsightSnapshotDto
            {
                GeneratedAt = now,
                BudgetHealth = budgetHealth,
                EvidenceSummary = $"Based on {receipts.Count} receipts, {budgets.Count} budgets, and {subscriptions.Count} recurring-spend signals.",
                MonthSpend = monthSpend,
                RecentAverage = recentAverage,
                TopCategory = topCategory,
                Anomalies = anomalies
                    .Concat(alerts
                        .Where(alert => alert.Severity == "warning" || alert.Severity == "critical")
                        .Select(alert => alert.Detail))
                    .Distinct()
                    .Take(4)
                    .ToList(),
                Suggestions = suggestions.Distinct().Take(4).ToList(),
                Insights = insights,
                Alerts = alerts,
                Subscriptions = subscriptions
            };

            _cache.Set(cacheKey, snapshot, TimeSpan.FromSeconds(20));
            return snapshot;
        }

        public async Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("subscriptions", userId);
            if (_cache.TryGetValue(cacheKey, out List<AiSubscriptionInsightDto>? cachedSubscriptions) && cachedSubscriptions != null)
            {
                return cachedSubscriptions;
            }

            var receipts = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.UploadedAt >= DateTime.UtcNow.AddMonths(-6))
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();

            var subscriptions = DetectSubscriptions(receipts);
            _cache.Set(cacheKey, subscriptions, TimeSpan.FromMinutes(2));
            return subscriptions;
        }

        public async Task<AiChatResponseDto> ChatAsync(int userId, string message)
        {
            var snapshot = await GetInsightsAsync(userId);
            var normalizedMessage = NormalizeChatMessage(message);
            var lowerMessage = normalizedMessage.ClassificationText.ToLowerInvariant();
            var referencedMetrics = new List<string>();
            var cards = new List<AiCopilotCardDto>();
            var alerts = snapshot.Alerts.Take(3).ToList();
            string reply;

            if (IsGreeting(lowerMessage))
            {
                referencedMetrics.Add("Workspace help");
                reply = "Hello. I can help with spending questions, app workflow, budgets, receipts, subscriptions, and what-if planning using your tracker data when it is relevant.";
                cards = BuildGeneralCards(snapshot);
            }
            else if (LooksLikeAppHelpQuestion(lowerMessage))
            {
                referencedMetrics.Add("App workflow");
                reply = "Use Dashboard for the overall picture, Receipts to upload and clean transaction history, Budgets to set limits and review pace, Categories to improve labeling rules, Profile for account settings, and Admin to manage users and workspace oversight.";
                cards = BuildGeneralCards(snapshot);
            }
            else if (lowerMessage.Contains("subscription") || lowerMessage.Contains("recurring") || lowerMessage.Contains("cancel"))
            {
                referencedMetrics.Add("Recurring spend");
                referencedMetrics.Add("Next due vendors");

                if (snapshot.Subscriptions.Count == 0)
                {
                    reply = "I do not see strong recurring subscription patterns yet. Upload a few more monthly receipts from the same vendors and I will start flagging likely subscriptions.";
                }
                else
                {
                    var leadingSubscription = snapshot.Subscriptions[0];
                    reply = $"{snapshot.Subscriptions.Count} recurring spend signals stand out. {leadingSubscription.Vendor} looks {leadingSubscription.Frequency.ToLowerInvariant()} at about {leadingSubscription.EstimatedMonthlyCost:C} per month.";
                }

                cards = BuildSubscriptionCards(snapshot);
            }
            else if (lowerMessage.Contains("budget") || lowerMessage.Contains("overspend") || lowerMessage.Contains("limit") || lowerMessage.Contains("forecast"))
            {
                referencedMetrics.Add("Budget health");
                referencedMetrics.Add("Month spend");
                referencedMetrics.Add("Projected spend");
                reply = snapshot.BudgetHealth == "No active budget"
                    ? "You do not have an active budget yet, so the biggest improvement is creating a General budget first. Once that is in place I can warn you before you overspend."
                    : $"Your current budget status is '{snapshot.BudgetHealth}'. Month-to-date spend is {snapshot.MonthSpend:C}. Focus first on {snapshot.TopCategory} because it is contributing the most pressure right now.";
                cards = BuildBudgetCards(snapshot);
            }
            else if (lowerMessage.Contains("category") || lowerMessage.Contains("spend most"))
            {
                referencedMetrics.Add("Top category");
                reply = snapshot.TopCategory == "N/A"
                    ? "I do not have enough categorized receipts yet to name a dominant category. Upload a few more receipts or correct categories in the receipts page."
                    : $"{snapshot.TopCategory} is your strongest spending signal right now. Review that category first if you want the fastest impact on this month's total.";
                cards = BuildCategoryCards(snapshot);
            }
            else if (lowerMessage.Contains("alert") || lowerMessage.Contains("risk") || lowerMessage.Contains("anomaly"))
            {
                referencedMetrics.Add("Active alerts");
                reply = alerts.Count == 0
                    ? "No major alert is active right now. Your tracker looks relatively stable from the latest receipts and budgets."
                    : $"{alerts[0].Title}: {alerts[0].Detail}";
                cards = BuildAlertCards(snapshot);
            }
            else if (lowerMessage.Contains("receipt") || lowerMessage.Contains("vendor") || lowerMessage.Contains("merchant"))
            {
                referencedMetrics.Add("Recent uploads");
                reply = snapshot.Insights.FirstOrDefault(x => x.Title == "Receipt activity")?.Summary
                    ?? "I do not have recent receipt activity to summarize yet.";
                cards = BuildReceiptCards(snapshot);
            }
            else
            {
                referencedMetrics.Add("Budget health");
                referencedMetrics.Add("Top category");
                referencedMetrics.Add("Recurring spend");
                reply = LooksLikeDecisionOrScenarioQuestion(lowerMessage)
                    ? $"I can help think through that using your current tracker picture. Right now {snapshot.BudgetHealth}, month spend is {snapshot.MonthSpend:C}, and {snapshot.TopCategory} is the leading category. Start with: {snapshot.Suggestions.FirstOrDefault() ?? "upload more receipts for a stronger signal."}"
                    : $"Here is the current picture: {snapshot.BudgetHealth}, month spend is {snapshot.MonthSpend:C}, and {snapshot.TopCategory} is the leading category. Start with: {snapshot.Suggestions.FirstOrDefault() ?? "upload more receipts for a stronger signal."}";
                cards = BuildGeneralCards(snapshot);
            }

            reply = await TryGenerateModelReplyAsync(normalizedMessage.UserQuestion, snapshot, reply);

            return new AiChatResponseDto
            {
                Reply = reply,
                EvidenceSummary = snapshot.EvidenceSummary,
                Suggestions = snapshot.Suggestions.Take(3).ToList(),
                ReferencedMetrics = referencedMetrics,
                Cards = cards,
                Alerts = alerts,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private static (string UserQuestion, string ClassificationText) NormalizeChatMessage(string rawMessage)
        {
            var trimmedMessage = rawMessage?.Trim() ?? string.Empty;
            const string userRequestPrefix = "User request:";
            const string assistantGuidanceMarker = "\n\nAssistant guidance:";

            if (!trimmedMessage.StartsWith(userRequestPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return (trimmedMessage, trimmedMessage);
            }

            var content = trimmedMessage[userRequestPrefix.Length..].Trim();
            var guidanceIndex = content.IndexOf(assistantGuidanceMarker, StringComparison.OrdinalIgnoreCase);
            if (guidanceIndex >= 0)
            {
                content = content[..guidanceIndex].Trim();
            }

            return (content, content);
        }

        private static bool IsGreeting(string lowerMessage)
        {
            var normalized = lowerMessage.Trim();
            return normalized is "hi" or "hello" or "hey" or "good morning" or "good evening";
        }

        private static bool LooksLikeAppHelpQuestion(string lowerMessage)
        {
            return lowerMessage.Contains("how do i use")
                || lowerMessage.Contains("how to use")
                || lowerMessage.Contains("which page")
                || lowerMessage.Contains("where do i")
                || lowerMessage.Contains("how do i add")
                || lowerMessage.Contains("how do i upload")
                || lowerMessage.Contains("how do i create a budget")
                || lowerMessage.Contains("how do i manage")
                || lowerMessage.Contains("what does this app do");
        }

        private static bool LooksLikeDecisionOrScenarioQuestion(string lowerMessage)
        {
            return lowerMessage.Contains("what if")
                || lowerMessage.Contains("can i afford")
                || lowerMessage.Contains("should i")
                || lowerMessage.Contains("is it okay if")
                || lowerMessage.Contains("weekend")
                || lowerMessage.Contains("trip")
                || lowerMessage.Contains("outing")
                || lowerMessage.Contains("vacation")
                || lowerMessage.Contains("plan");
        }

        private static string GetUserCacheKey(string scope, int userId) =>
            $"ai:{scope}:{userId}";

        private async Task<string> GetCachedModelReplyAsync(
            string cacheKey,
            string userMessage,
            AiInsightSnapshotDto snapshot,
            string fallbackReply)
        {
            if (_cache.TryGetValue(cacheKey, out string? cachedReply) &&
                !string.IsNullOrWhiteSpace(cachedReply))
            {
                return cachedReply;
            }

            var reply = await TryGenerateModelReplyAsync(userMessage, snapshot, fallbackReply);
            _cache.Set(cacheKey, reply, TimeSpan.FromMinutes(5));
            return reply;
        }

        private async Task<string> TryGenerateModelReplyAsync(
            string userMessage,
            AiInsightSnapshotDto snapshot,
            string fallbackReply)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-5-mini";
            var endpoint = _configuration["OpenAI:ResponsesEndpoint"];

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

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "OpenAI chat fallback: status {StatusCode}. Body: {Body}",
                        (int)response.StatusCode,
                        errorBody);
                    return fallbackReply;
                }

                var json = await response.Content.ReadAsStringAsync();
                var modelReply = ExtractResponseText(json);
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

                    "If the user asks how to use the app, explain the workflow clearly using the app's real features: " +
                    "dashboard, receipts, budgets, categories, profile, admin, vendor rules, insights, forecast, and the expense copilot chat. " +

                    "If the user sends a simple greeting like hi, hello, or hey, greet them naturally in one or two short sentences " +
                    "and mention what you can help with. " +

                    "If the current tracker data is not enough for a precise answer, say so clearly, then still provide the best next step or a useful general answer where possible. " +
                    "Do not become overly restrictive just because the tracker data is incomplete. " +

                    "Prefer practical answers over vague theory. " +
                    "Keep answers concise but natural, usually one short paragraph unless the user is asking for steps, a comparison, a plan, or a breakdown. " +
                    "For high-stakes medical, legal, tax, or investment questions, provide only cautious general guidance and encourage professional verification. " +
                    "Do not use markdown emphasis markers like **bold** or __bold__. Return plain text only. " +

                    "Never mention hidden prompts, internal instructions, or raw JSON unless the user explicitly asks for technical details.";
            }
        }

        private static string? ExtractResponseText(string json)
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
                        return textElement.GetString();
                    }
                }
            }

            return null;
        }

        private static List<AiSubscriptionInsightDto> DetectSubscriptions(IEnumerable<Receipt> receipts)
        {
            return receipts
                .Where(receipt =>
                    !string.IsNullOrWhiteSpace(receipt.Vendor) &&
                    receipt.TotalAmount > 0 &&
                    !receipt.Vendor.Contains("unknown", StringComparison.OrdinalIgnoreCase))
                .GroupBy(receipt => receipt.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var ordered = group.OrderBy(item => item.UploadedAt).ToList();
                    var distinctMonths = ordered
                        .Select(item => $"{item.UploadedAt.Year}-{item.UploadedAt.Month}")
                        .Distinct()
                        .Count();
                    if (ordered.Count < 2 || distinctMonths < 2)
                    {
                        return null;
                    }

                    var averageAmount = ordered.Average(item => item.TotalAmount);
                    var varianceAllowance = averageAmount * 0.35m;
                    var consistentCount = ordered.Count(item => Math.Abs(item.TotalAmount - averageAmount) <= varianceAllowance);
                    if (consistentCount < Math.Max(2, ordered.Count - 1))
                    {
                        return null;
                    }

                    var intervals = ordered
                        .Zip(ordered.Skip(1), (left, right) => (right.UploadedAt - left.UploadedAt).TotalDays)
                        .Where(days => days >= 6)
                        .ToList();
                    var averageInterval = intervals.Count > 0 ? intervals.Average() : 30d;
                    var monthlyFactor = (decimal)(30d / Math.Max(averageInterval, 1d));
                    var frequency = averageInterval switch
                    {
                        <= 12d => "Weekly",
                        <= 45d => "Monthly",
                        <= 100d => "Quarterly",
                        _ => "Recurring"
                    };

                    return new AiSubscriptionInsightDto
                    {
                        Vendor = ordered.Last().Vendor?.Trim() ?? group.Key,
                        Category = ordered
                            .Select(item => item.Category)
                            .FirstOrDefault(category => !string.IsNullOrWhiteSpace(category))
                            ?? "Recurring",
                        AverageAmount = Math.Round(averageAmount, 2, MidpointRounding.AwayFromZero),
                        EstimatedMonthlyCost = Math.Round(averageAmount * monthlyFactor, 2, MidpointRounding.AwayFromZero),
                        Frequency = frequency,
                        Occurrences = ordered.Count,
                        LastSeenAt = ordered.Last().UploadedAt,
                        NextExpectedDate = ordered.Last().UploadedAt.AddDays(Math.Round(averageInterval))
                    };
                })
                .Where(item => item != null)
                .Cast<AiSubscriptionInsightDto>()
                .OrderByDescending(item => item.EstimatedMonthlyCost)
                .ThenBy(item => item.Vendor)
                .Take(6)
                .ToList();
        }

        private static List<AiCopilotAlertDto> BuildAlerts(
            BudgetAdvisorSnapshotDto budgetAdvisor,
            IReadOnlyList<AiSubscriptionInsightDto> subscriptions,
            int uncategorizedReceiptCount,
            IReadOnlyCollection<Receipt> receipts)
        {
            var alerts = new List<AiCopilotAlertDto>();

            if (budgetAdvisor.PaceStatus == "critical")
            {
                alerts.Add(new AiCopilotAlertDto
                {
                    Title = "Budget overspend projected",
                    Detail = $"Current pacing points to {budgetAdvisor.ProjectedSpend:C} against a {budgetAdvisor.TotalBudget:C} budget.",
                    Severity = "critical"
                });
            }
            else if (budgetAdvisor.PaceStatus == "warning")
            {
                alerts.Add(new AiCopilotAlertDto
                {
                    Title = "Budget getting tight",
                    Detail = $"Projected spend is nearing the monthly limit with only {budgetAdvisor.RemainingBudget:C} of headroom left.",
                    Severity = "warning"
                });
            }

            foreach (var category in budgetAdvisor.Categories
                         .Where(item => item.RiskLevel == "critical" || item.RiskLevel == "warning")
                         .Take(2))
            {
                alerts.Add(new AiCopilotAlertDto
                {
                    Title = $"{category.Category} needs attention",
                    Detail = category.Insight,
                    Severity = category.RiskLevel
                });
            }

            if (uncategorizedReceiptCount > 0)
            {
                alerts.Add(new AiCopilotAlertDto
                {
                    Title = "Receipt cleanup needed",
                    Detail = $"{uncategorizedReceiptCount} recent receipts are uncategorized, which weakens AI guidance.",
                    Severity = "warning"
                });
            }

            foreach (var subscription in subscriptions
                         .Where(item => item.NextExpectedDate.HasValue && item.NextExpectedDate.Value <= DateTime.UtcNow.AddDays(10))
                         .Take(2))
            {
                alerts.Add(new AiCopilotAlertDto
                {
                    Title = $"Upcoming recurring charge: {subscription.Vendor}",
                    Detail = $"Expected around {subscription.NextExpectedDate:MMM d} for about {subscription.AverageAmount:C}.",
                    Severity = "info"
                });
            }

            if (receipts.Count >= 8 && subscriptions.Count == 0)
            {
                alerts.Add(new AiCopilotAlertDto
                {
                    Title = "Recurring spend still learning",
                    Detail = "Upload a few more monthly receipts from the same vendors and the copilot will start detecting subscriptions.",
                    Severity = "info"
                });
            }

            return alerts
                .GroupBy(alert => $"{alert.Title}|{alert.Detail}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(alert => GetSeverityRank(alert.Severity))
                .ToList();
        }

        private static List<AiCopilotCardDto> BuildBudgetCards(AiInsightSnapshotDto snapshot)
        {
            var budgetPulse = snapshot.Insights.FirstOrDefault(insight => insight.Title == "Budget pulse");

            return new List<AiCopilotCardDto>
            {
                new()
                {
                    Title = "Budget health",
                    Value = snapshot.BudgetHealth,
                    Detail = budgetPulse?.Summary ?? "Budget insight is warming up.",
                    Tone = budgetPulse?.Severity == "critical" ? "critical" : budgetPulse?.Severity == "warning" ? "warning" : "positive"
                },
                new()
                {
                    Title = "Month spend",
                    Value = $"{snapshot.MonthSpend:C}",
                    Detail = "Total spend detected in the current month.",
                    Tone = "default"
                },
                new()
                {
                    Title = "Category pressure",
                    Value = snapshot.TopCategory,
                    Detail = "Current top category contributing to spend.",
                    Tone = snapshot.TopCategory == "N/A" ? "default" : "warning"
                }
            };
        }

        private static List<AiCopilotCardDto> BuildSubscriptionCards(AiInsightSnapshotDto snapshot)
        {
            var lead = snapshot.Subscriptions.FirstOrDefault();

            return new List<AiCopilotCardDto>
            {
                new()
                {
                    Title = "Recurring vendors",
                    Value = snapshot.Subscriptions.Count.ToString(),
                    Detail = "Detected from repeat merchant patterns in receipt history.",
                    Tone = snapshot.Subscriptions.Count > 0 ? "positive" : "default"
                },
                new()
                {
                    Title = "Recurring monthly total",
                    Value = $"{snapshot.Subscriptions.Sum(item => item.EstimatedMonthlyCost):C}",
                    Detail = "Estimated monthly cost from likely subscriptions.",
                    Tone = "warning"
                },
                new()
                {
                    Title = "Next due vendor",
                    Value = lead?.Vendor ?? "No signal",
                    Detail = lead?.NextExpectedDate is DateTime date
                        ? $"Expected around {date:MMM d}"
                        : "Need more recurring history to predict the next billing date.",
                    Tone = "default"
                }
            };
        }

        private static List<AiCopilotCardDto> BuildCategoryCards(AiInsightSnapshotDto snapshot)
        {
            return new List<AiCopilotCardDto>
            {
                new()
                {
                    Title = "Top category",
                    Value = snapshot.TopCategory,
                    Detail = "Largest categorized spend signal this month.",
                    Tone = snapshot.TopCategory == "N/A" ? "default" : "positive"
                },
                new()
                {
                    Title = "Recent average",
                    Value = $"{snapshot.RecentAverage:C}",
                    Detail = "Average of your recent tracked months.",
                    Tone = "default"
                },
                new()
                {
                    Title = "Active anomalies",
                    Value = snapshot.Anomalies.Count.ToString(),
                    Detail = "Signals the copilot wants you to review.",
                    Tone = snapshot.Anomalies.Count > 0 ? "warning" : "positive"
                }
            };
        }

        private static List<AiCopilotCardDto> BuildReceiptCards(AiInsightSnapshotDto snapshot)
        {
            var receiptActivity = snapshot.Insights.FirstOrDefault(insight => insight.Title == "Receipt activity");

            return new List<AiCopilotCardDto>
            {
                new()
                {
                    Title = "Recent uploads",
                    Value = receiptActivity?.MetricValue ?? "0",
                    Detail = receiptActivity?.Summary ?? "No recent receipt activity.",
                    Tone = "default"
                },
                new()
                {
                    Title = "Top category",
                    Value = snapshot.TopCategory,
                    Detail = "Most visible category in recent receipt activity.",
                    Tone = snapshot.TopCategory == "N/A" ? "default" : "positive"
                },
                new()
                {
                    Title = "Receipt alerts",
                    Value = snapshot.Alerts.Count.ToString(),
                    Detail = "Alerts currently tied to receipts or pacing.",
                    Tone = snapshot.Alerts.Count > 0 ? "warning" : "positive"
                }
            };
        }

        private static List<AiCopilotCardDto> BuildAlertCards(AiInsightSnapshotDto snapshot)
        {
            return snapshot.Alerts.Take(3).Select(alert => new AiCopilotCardDto
            {
                Title = alert.Title,
                Value = alert.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase) ? "High" :
                    alert.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? "Watch" : "Info",
                Detail = alert.Detail,
                Tone = alert.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)
                    ? "critical"
                    : alert.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
                        ? "warning"
                        : "default"
            }).ToList();
        }

        private static List<AiCopilotCardDto> BuildGeneralCards(AiInsightSnapshotDto snapshot)
        {
            return new List<AiCopilotCardDto>
            {
                new()
                {
                    Title = "Budget health",
                    Value = snapshot.BudgetHealth,
                    Detail = "Grounded in your live budget and receipt activity.",
                    Tone = snapshot.Anomalies.Count > 0 ? "warning" : "positive"
                },
                new()
                {
                    Title = "Top category",
                    Value = snapshot.TopCategory,
                    Detail = "Current month leader across categorized receipts.",
                    Tone = snapshot.TopCategory == "N/A" ? "default" : "positive"
                },
                new()
                {
                    Title = "Recurring spend",
                    Value = $"{snapshot.Subscriptions.Sum(item => item.EstimatedMonthlyCost):C}",
                    Detail = $"{snapshot.Subscriptions.Count} recurring merchant signals detected.",
                    Tone = snapshot.Subscriptions.Count > 0 ? "warning" : "default"
                }
            };
        }

        private static int GetSeverityRank(string severity) => severity switch
        {
            "critical" => 3,
            "warning" => 2,
            "positive" => 1,
            _ => 0
        };

        private async Task<ForecastComputation> BuildForecastAsync(int userId, DateTime referenceUtc)
        {
            var monthStart = new DateTime(referenceUtc.Year, referenceUtc.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(referenceUtc.Year, referenceUtc.Month);
            var daysElapsed = Math.Max(1, (referenceUtc - monthStart).Days + 1);
            var daysRemaining = Math.Max(daysInMonth - daysElapsed, 0);

            var dailySpending = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= monthStart && r.UploadedAt < monthEnd)
                .GroupBy(r => r.UploadedAt.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Amount = group.Sum(item => item.TotalAmount)
                })
                .OrderBy(item => item.Date)
                .ToListAsync();

            var drivers = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r =>
                    r.UserId == userId &&
                    r.UploadedAt >= monthStart &&
                    r.UploadedAt < monthEnd &&
                    !string.IsNullOrWhiteSpace(r.Category))
                .GroupBy(r => r.Category!)
                .Select(group => new ForecastDriverDto
                {
                    Category = group.Key,
                    Amount = Math.Round(group.Sum(item => item.TotalAmount), 2)
                })
                .OrderByDescending(item => item.Amount)
                .Take(3)
                .ToListAsync();

            var recentMonthlyTotals = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= referenceUtc.AddMonths(-3))
                .GroupBy(r => new { r.UploadedAt.Year, r.UploadedAt.Month })
                .Select(group => group.Sum(item => item.TotalAmount))
                .ToListAsync();

            var currentSpend = dailySpending.Sum(item => item.Amount);
            var dailyAverage = daysElapsed > 0 ? currentSpend / daysElapsed : 0m;
            var projectedMonthEnd = currentSpend + (dailyAverage * daysRemaining);
            var budgetAdvisor = await _budgetAdvisorService.GetBudgetAdvisorAsync(userId, referenceUtc);
            var trend = GetForecastTrend(projectedMonthEnd, budgetAdvisor.TotalBudget);
            var topCategory = drivers.FirstOrDefault()?.Category ?? "N/A";
            var fallbackNarrative = trend == "critical"
                ? $"You're on pace to spend {projectedMonthEnd:C} this month. That exceeds your budget and deserves an immediate cutback plan."
                : trend == "warning"
                    ? $"Projected month-end spend of {projectedMonthEnd:C} is approaching your limit. Watch your top categories."
                    : $"Spending looks controlled at {dailyAverage:C}/day. Projected month-end total: {projectedMonthEnd:C}.";

            var snapshot = new AiInsightSnapshotDto
            {
                GeneratedAt = referenceUtc,
                BudgetHealth = trend == "critical"
                    ? "Over budget"
                    : trend == "warning"
                        ? "Approaching budget limit"
                        : "Healthy budget pace",
                EvidenceSummary = $"Forecast built from {dailySpending.Count} tracked spending day{(dailySpending.Count == 1 ? string.Empty : "s")} in {referenceUtc:MMMM yyyy}.",
                MonthSpend = RoundCurrency(currentSpend),
                RecentAverage = recentMonthlyTotals.Count > 0
                    ? RoundCurrency(recentMonthlyTotals.Average())
                    : 0m,
                TopCategory = topCategory,
                Suggestions = budgetAdvisor.Recommendations.Take(3).ToList()
            };

            var dailyByDate = dailySpending.ToDictionary(
                item => item.Date,
                item => RoundCurrency(item.Amount));

            var breakdown = new List<DailySpendPointDto>();
            for (var day = monthStart.Date; day <= referenceUtc.Date; day = day.AddDays(1))
            {
                breakdown.Add(new DailySpendPointDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Amount = dailyByDate.TryGetValue(day, out var amount) ? amount : 0m,
                    IsProjected = false
                });
            }

            for (var day = referenceUtc.Date.AddDays(1); day <= monthEnd.AddDays(-1); day = day.AddDays(1))
            {
                breakdown.Add(new DailySpendPointDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Amount = RoundCurrency(dailyAverage),
                    IsProjected = true
                });
            }

            return new ForecastComputation(
                referenceUtc,
                currentSpend,
                projectedMonthEnd,
                dailyAverage,
                daysElapsed,
                daysRemaining,
                trend,
                budgetAdvisor.TotalBudget,
                topCategory,
                drivers,
                breakdown,
                snapshot,
                fallbackNarrative,
                budgetAdvisor);
        }

        private static string GetForecastTrend(decimal projectedMonthEnd, decimal totalBudget)
        {
            if (totalBudget <= 0m)
            {
                return "on-track";
            }

            if (projectedMonthEnd >= totalBudget)
            {
                return "critical";
            }

            return projectedMonthEnd >= totalBudget * 0.8m
                ? "warning"
                : "on-track";
        }

        private static string GetForecastTrendLabel(string trend) => trend switch
        {
            "critical" => "Over budget pace",
            "warning" => "Approaching limit",
            _ => "On track"
        };

        private static string BuildWhatIfSummary(
            ForecastComputation forecast,
            decimal adjustedProjectedMonthEnd,
            string adjustedTrend,
            IReadOnlyCollection<ForecastAdjustmentDto> adjustments,
            decimal netChange)
        {
            var categories = string.Join(", ", adjustments.Select(adjustment =>
                $"{adjustment.Category} {(adjustment.DeltaAmount < 0 ? adjustment.DeltaAmount.ToString("C") : $"+{adjustment.DeltaAmount:C}")}"));
            var changeSummary = netChange < 0
                ? $"cuts projected spend by {Math.Abs(netChange):C}"
                : $"adds {netChange:C} to projected spend";

            return $"{categories} {changeSummary}, moving your month-end projection from {forecast.ProjectedMonthEnd:C} to {adjustedProjectedMonthEnd:C}. Risk shifts from {GetForecastTrendLabel(forecast.Trend).ToLowerInvariant()} to {GetForecastTrendLabel(adjustedTrend).ToLowerInvariant()}.";
        }

        private static decimal RoundCurrency(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

        public async Task<List<SpendingAnomalyDto>> GetSpendingAnomaliesAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("spending-anomalies", userId);
            if (_cache.TryGetValue(cacheKey, out List<SpendingAnomalyDto>? cachedAnomalies) && cachedAnomalies != null)
            {
                return cachedAnomalies;
            }

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var threeMonthsBack = now.AddMonths(-3);

            var receipts = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= threeMonthsBack)
                .ToListAsync();

            var thisMonthByCategory = receipts
                .Where(r => r.UploadedAt >= monthStart && !string.IsNullOrWhiteSpace(r.Category))
                .GroupBy(r => r.Category!)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));

            var priorByCategory = receipts
                .Where(r => r.UploadedAt < monthStart && !string.IsNullOrWhiteSpace(r.Category))
                .GroupBy(r => r.Category!)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount) / 3m);

            var anomalies = new List<SpendingAnomalyDto>();

            foreach (KeyValuePair<string, decimal> entry in thisMonthByCategory)
            {
                if (!priorByCategory.TryGetValue(entry.Key, out var avgMonth) || avgMonth <= 0)
                    continue;

                var pctIncrease = ((entry.Value - avgMonth) / avgMonth) * 100m;

                if (pctIncrease < 20)
                    continue;

                anomalies.Add(new SpendingAnomalyDto
                {
                    Category = entry.Key,
                    ThisMonth = Math.Round(entry.Value, 2),
                    AverageMonth = Math.Round(avgMonth, 2),
                    PercentageIncrease = Math.Round(pctIncrease, 1),
                    Severity = pctIncrease >= 100 ? "critical" : pctIncrease >= 50 ? "warning" : "normal",
                    Message = $"{entry.Key} is up {pctIncrease:F0}% vs. your 3-month average (${avgMonth:F0} → ${entry.Value:F0})."
                });
            }

            var results = anomalies.OrderByDescending(a => a.PercentageIncrease).Take(5).ToList();
            _cache.Set(cacheKey, results, TimeSpan.FromSeconds(20));
            return results;
        }

        public async Task<MonthlySummaryDto> GetMonthlySummaryAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("monthly-summary", userId);
            if (_cache.TryGetValue(cacheKey, out MonthlySummaryDto? cachedSummary) && cachedSummary != null)
            {
                return cachedSummary;
            }

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var receipts = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= monthStart)
                .ToListAsync();

            var totalSpend = receipts.Sum(r => r.TotalAmount);
            var topCategory = receipts
                .Where(r => !string.IsNullOrWhiteSpace(r.Category))
                .GroupBy(r => r.Category!)
                .OrderByDescending(g => g.Sum(r => r.TotalAmount))
                .FirstOrDefault()?.Key ?? "N/A";

            var anomalies = await GetSpendingAnomaliesAsync(userId);

            var aiSummary = anomalies.Count > 0
                ? $"This month you've spent ${totalSpend:F0} with {receipts.Count} receipts. Top category: {topCategory}. Notable: {anomalies.First().Message}"
                : $"This month you've spent ${totalSpend:F0} across {receipts.Count} receipts. Top category: {topCategory}. Spending looks steady with no major anomalies.";

            var summary = new MonthlySummaryDto
            {
                Month = now.ToString("MMMM yyyy"),
                TotalSpend = Math.Round(totalSpend, 2),
                TopCategory = topCategory,
                ReceiptCount = receipts.Count,
                AiSummary = aiSummary,
                Anomalies = anomalies
            };

            _cache.Set(cacheKey, summary, TimeSpan.FromSeconds(20));
            return summary;
        }

        private async Task<SpendingForecastDto> BuildSpendingForecastLegacyAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var daysElapsed = Math.Max(1, (now - monthStart).Days + 1);
            var daysRemaining = Math.Max(daysInMonth - daysElapsed, 0);

            var dailySpending = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= monthStart && r.UploadedAt < monthEnd)
                .GroupBy(r => r.UploadedAt.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Amount = group.Sum(item => item.TotalAmount)
                })
                .OrderBy(item => item.Date)
                .ToListAsync();

            var currentSpend = dailySpending.Sum(item => item.Amount);
            var dailyAverage = currentSpend / daysElapsed;
            var projectedMonthEnd = currentSpend + (dailyAverage * daysRemaining);

            var budgetAdvisor = await _budgetAdvisorService.GetBudgetAdvisorAsync(userId, now);
            var trend = budgetAdvisor.TotalBudget > 0
                ? projectedMonthEnd >= budgetAdvisor.TotalBudget * 1.0m
                    ? "critical"
                    : projectedMonthEnd >= budgetAdvisor.TotalBudget * 0.8m
                        ? "warning"
                        : "on-track"
                : "on-track";

            var fallbackNarrative = trend == "critical"
                ? $"You're on pace to spend {projectedMonthEnd:C} this month. That exceeds your budget — consider pausing discretionary expenses."
                : trend == "warning"
                    ? $"Projected month-end spend of {projectedMonthEnd:C} is approaching your limit. Watch your top categories."
                    : $"Spending looks controlled at {dailyAverage:C}/day. Projected month-end total: {projectedMonthEnd:C}.";

            var topCategory = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r =>
                    r.UserId == userId &&
                    r.UploadedAt >= monthStart &&
                    r.UploadedAt < monthEnd &&
                    !string.IsNullOrWhiteSpace(r.Category))
                .GroupBy(r => r.Category!)
                .Select(group => new
                {
                    Category = group.Key,
                    Total = group.Sum(item => item.TotalAmount)
                })
                .OrderByDescending(item => item.Total)
                .Select(item => item.Category)
                .FirstOrDefaultAsync() ?? "N/A";

            var recentMonthlyTotals = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= now.AddMonths(-3))
                .GroupBy(r => new { r.UploadedAt.Year, r.UploadedAt.Month })
                .Select(group => group.Sum(item => item.TotalAmount))
                .ToListAsync();

            var forecastSnapshot = new AiInsightSnapshotDto
            {
                GeneratedAt = now,
                BudgetHealth = trend == "critical"
                    ? "Over budget"
                    : trend == "warning"
                        ? "Approaching budget limit"
                        : "Healthy budget pace",
                EvidenceSummary = $"Forecast built from {dailySpending.Count} tracked spending day{(dailySpending.Count == 1 ? string.Empty : "s")} in {now:MMMM yyyy}.",
                MonthSpend = Math.Round(currentSpend, 2),
                RecentAverage = recentMonthlyTotals.Count > 0
                    ? Math.Round(recentMonthlyTotals.Average(), 2)
                    : 0m,
                TopCategory = topCategory,
                Suggestions = budgetAdvisor.Recommendations.Take(3).ToList()
            };

            var narrative = await GetCachedModelReplyAsync(
                $"{GetUserCacheKey("forecast-narrative", userId)}:{now:yyyyMM}:{Math.Round(currentSpend, 2)}:{Math.Round(projectedMonthEnd, 2)}:{Math.Round(budgetAdvisor.TotalBudget, 2)}:{trend}",
                $"Forecast: currently at {currentSpend:C} after {daysElapsed} days. Projected end: {projectedMonthEnd:C}. Budget: {budgetAdvisor.TotalBudget:C}. Give a brief 1-sentence spending forecast advice.",
                forecastSnapshot,
                fallbackNarrative);

            var dailyByDate = dailySpending.ToDictionary(
                item => item.Date,
                item => Math.Round(item.Amount, 2));

            var breakdown = new List<DailySpendPointDto>();
            for (var d = monthStart.Date; d <= now.Date; d = d.AddDays(1))
            {
                breakdown.Add(new DailySpendPointDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Amount = dailyByDate.TryGetValue(d, out var amt) ? Math.Round(amt, 2) : 0,
                    IsProjected = false
                });
            }
            for (var d = now.Date.AddDays(1); d <= monthStart.AddMonths(1).AddDays(-1); d = d.AddDays(1))
            {
                breakdown.Add(new DailySpendPointDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Amount = Math.Round(dailyAverage, 2),
                    IsProjected = true
                });
            }

            return new SpendingForecastDto
            {
                CurrentSpend = Math.Round(currentSpend, 2),
                ProjectedMonthEnd = Math.Round(projectedMonthEnd, 2),
                DailyAverage = Math.Round(dailyAverage, 2),
                DaysElapsed = daysElapsed,
                DaysRemaining = daysRemaining,
                Trend = trend,
                AiNarrative = narrative,
                DailyBreakdown = breakdown
            };
        }

        public async Task<SpendingForecastDto> GetSpendingForecastAsync(int userId)
        {
            var forecast = await BuildForecastAsync(userId, DateTime.UtcNow);
            var narrative = await GetCachedModelReplyAsync(
                $"{GetUserCacheKey("forecast-narrative", userId)}:{forecast.ReferenceUtc:yyyyMM}:{RoundCurrency(forecast.CurrentSpend)}:{RoundCurrency(forecast.ProjectedMonthEnd)}:{RoundCurrency(forecast.TotalBudget)}:{forecast.Trend}",
                $"Forecast: currently at {forecast.CurrentSpend:C} after {forecast.DaysElapsed} days. Projected end: {forecast.ProjectedMonthEnd:C}. Budget: {forecast.TotalBudget:C}. Give a brief 1-sentence spending forecast advice.",
                forecast.Snapshot,
                forecast.FallbackNarrative);

            return new SpendingForecastDto
            {
                CurrentSpend = RoundCurrency(forecast.CurrentSpend),
                ProjectedMonthEnd = RoundCurrency(forecast.ProjectedMonthEnd),
                DailyAverage = RoundCurrency(forecast.DailyAverage),
                DaysElapsed = forecast.DaysElapsed,
                DaysRemaining = forecast.DaysRemaining,
                Trend = forecast.Trend,
                AiNarrative = narrative,
                BudgetAmount = RoundCurrency(forecast.TotalBudget),
                TopCategory = forecast.TopCategory,
                Drivers = forecast.Drivers,
                DailyBreakdown = forecast.DailyBreakdown
            };
        }

        public async Task<WhatIfForecastDto> GetWhatIfForecastAsync(int userId, WhatIfForecastRequestDto request)
        {
            var forecast = await BuildForecastAsync(userId, DateTime.UtcNow);
            var adjustments = (request?.Adjustments ?? new List<ForecastAdjustmentDto>())
                .Where(adjustment =>
                    !string.IsNullOrWhiteSpace(adjustment.Category) &&
                    adjustment.DeltaAmount != 0)
                .GroupBy(adjustment => adjustment.Category.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new ForecastAdjustmentDto
                {
                    Category = group.First().Category.Trim(),
                    DeltaAmount = RoundCurrency(group.Sum(item => item.DeltaAmount))
                })
                .ToList();

            var netChange = adjustments.Sum(item => item.DeltaAmount);
            var adjustedProjectedMonthEnd = Math.Max(0m, forecast.ProjectedMonthEnd + netChange);
            var adjustedTrend = GetForecastTrend(adjustedProjectedMonthEnd, forecast.TotalBudget);

            return new WhatIfForecastDto
            {
                BaseProjectedMonthEnd = RoundCurrency(forecast.ProjectedMonthEnd),
                AdjustedProjectedMonthEnd = RoundCurrency(adjustedProjectedMonthEnd),
                NetChange = RoundCurrency(netChange),
                Trend = adjustedTrend,
                Summary = adjustments.Count == 0
                    ? "Add a category adjustment to see how your projected month-end total changes."
                    : BuildWhatIfSummary(forecast, adjustedProjectedMonthEnd, adjustedTrend, adjustments, netChange),
                Adjustments = adjustments
            };
        }

        public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("weekly-summary", userId);
            if (_cache.TryGetValue(cacheKey, out WeeklySummaryDto? cachedSummary) && cachedSummary != null)
            {
                return cachedSummary;
            }

            var now = DateTime.UtcNow;
            var weekStart = now.Date.AddDays(-6);
            var weekEnd = now.Date.AddDays(1);

            var receipts = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= weekStart && r.UploadedAt < weekEnd)
                .ToListAsync();

            var totalSpend = receipts.Sum(r => r.TotalAmount);
            var topCategories = receipts
                .Where(r => !string.IsNullOrWhiteSpace(r.Category))
                .GroupBy(r => r.Category!)
                .Select(group => new WeeklyCategorySpendDto
                {
                    Category = group.Key,
                    TotalSpend = RoundCurrency(group.Sum(item => item.TotalAmount))
                })
                .OrderByDescending(item => item.TotalSpend)
                .Take(3)
                .ToList();

            var forecast = await BuildForecastAsync(userId, now);
            var summary = new WeeklySummaryDto
            {
                RangeLabel = $"{weekStart:MMM d} - {now:MMM d}",
                TotalSpend = RoundCurrency(totalSpend),
                ReceiptCount = receipts.Count,
                TopCategory = topCategories.FirstOrDefault()?.Category ?? "N/A",
                ForecastRisk = GetForecastTrendLabel(forecast.Trend),
                Recommendation = receipts.Count == 0
                    ? "Start logging expenses this week so the assistant can build a meaningful digest."
                    : forecast.BudgetAdvisor.Recommendations.FirstOrDefault()
                        ?? "Keep categorizing expenses consistently so next week's summary gets sharper.",
                TopCategories = topCategories
            };

            _cache.Set(cacheKey, summary, TimeSpan.FromMinutes(2));
            return summary;
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("notifications", userId);
            if (_cache.TryGetValue(cacheKey, out List<NotificationDto>? cachedNotifications) && cachedNotifications != null)
            {
                return cachedNotifications;
            }

            var notifications = new List<NotificationDto>();
            var now = DateTime.UtcNow;
            var preferences = await _unitOfWork.Users.Query()
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new NotificationPreferenceSnapshot(
                    user.BudgetNotificationsEnabled,
                    user.AnomalyNotificationsEnabled,
                    user.SubscriptionNotificationsEnabled))
                .FirstOrDefaultAsync();

            if (preferences == null)
            {
                return notifications;
            }

            var alerts = (await GetInsightsAsync(userId)).Alerts;
            foreach (var alert in alerts.Take(5))
            {
                notifications.Add(new NotificationDto
                {
                    Id = $"alert-{alert.Title.GetHashCode():x}",
                    Title = alert.Title,
                    Message = alert.Detail,
                    Type = alert.Severity == "critical" || alert.Severity == "warning" ? "budget" : "info",
                    Severity = alert.Severity,
                    GeneratedAt = now
                });
            }

            var anomalies = await GetSpendingAnomaliesAsync(userId);
            foreach (var anomaly in anomalies.Where(a => a.Severity != "normal").Take(3))
            {
                notifications.Add(new NotificationDto
                {
                    Id = $"anomaly-{anomaly.Category.GetHashCode():x}",
                    Title = $"Spending spike: {anomaly.Category}",
                    Message = anomaly.Message,
                    Type = "anomaly",
                    Severity = anomaly.Severity,
                    GeneratedAt = now
                });
            }

            var subscriptions = await GetSubscriptionsAsync(userId);
            foreach (var sub in subscriptions.Where(s => s.NextExpectedDate.HasValue && s.NextExpectedDate.Value <= now.AddDays(7)).Take(2))
            {
                notifications.Add(new NotificationDto
                {
                    Id = $"sub-{sub.Vendor.GetHashCode():x}",
                    Title = $"Upcoming charge: {sub.Vendor}",
                    Message = $"Expected around {sub.NextExpectedDate:MMM d} for approx. {sub.AverageAmount:C}.",
                    Type = "subscription",
                    Severity = "info",
                    GeneratedAt = now
                });
            }

            var results = notifications
                .Where(notification => notification.Type switch
                {
                    "budget" => preferences.BudgetNotificationsEnabled,
                    "anomaly" => preferences.AnomalyNotificationsEnabled,
                    "subscription" => preferences.SubscriptionNotificationsEnabled,
                    _ => true
                })
                .OrderByDescending(n => GetSeverityRank(n.Severity))
                .ThenByDescending(n => n.GeneratedAt)
                .ToList();

            _cache.Set(cacheKey, results, TimeSpan.FromSeconds(15));
            return results;
        }

        public async Task<ParseTextResultDto> ParseTextExpenseAsync(string text)
        {
            var fallback = new ParseTextResultDto
            {
                Vendor = "Unknown",
                Amount = 0,
                Category = "General",
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Parsed = false,
                RawText = text
            };

            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-5-mini";
            var endpoint = _configuration["OpenAI:ResponsesEndpoint"];

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
                    instructions = @"You are a smart global expense receipt parser. Extract structured expense data from informal, conversational, or typoed text in ANY language or country.

                    CATEGORIES — pick the single best match using common sense and context:

                    Daily spending:
                    - Food & Dining: restaurants, cafes, dhabas, bars, food courts, hotel meals, takeaway, dine-in
                    - Snacks & Beverages: chocolates, chips, candy, street food (egg roll, momos, vada pav, puchka, hot dog), juice, chai, coffee (takeaway), ice cream, biscuits, namkeen
                    - Groceries: vegetables, fruits, dairy, rice, dal, atta, supermarkets (Walmart, Tesco, Big Bazaar, DMart, Carrefour, Lidl), kirana store, corner shop
                    - Food Delivery: Swiggy, Zomato, Uber Eats, DoorDash, Deliveroo, Grab Food, FoodPanda, Talabat, online food order

                    Getting around:
                    - Transport: taxi, auto, rickshaw, Ola, Uber, Lyft, Grab, Rapido, bus, metro, train, ferry, toll, parking, petrol, diesel, fuel, EV charging, bike rental
                    - Travel & Trips: flights, hotels (overnight stay), Airbnb, hostels, vacation, tours, sightseeing, travel insurance, visa fees, holiday packages

                    Home & living:
                    - Rent & Housing: house rent, room rent, PG, apartment, office rent, hostel rent, society charges, maintenance, lease, security deposit
                    - Utilities & Bills: electricity, water, gas (Indane, HP, British Gas), internet, mobile recharge, broadband, DTH, cable TV, WiFi, phone bill
                    - Home & Furniture: furniture, appliances, home decor, home repair, plumber, electrician, carpenter, painting, AC service, pest control, IKEA
                    - Pets: pet food, vet, grooming, pet supplies, pet medicine, boarding, kennel

                    Health:
                    - Healthcare & Medicine: pharmacy (Apollo, CVS, Boots, MedPlus, Walgreens), doctor, hospital, clinic, lab test, dental, optician, health checkup, ambulance, surgery, consultation
                    - Fitness & Wellness: gym, yoga, salon, spa, haircut, parlour, massage, sports equipment, swimming pool, fitness classes, dietitian

                    Shopping:
                    - Shopping & Clothing: clothes, shoes, bags, accessories, gifts, Amazon, Flipkart, Myntra, ASOS, Zara, H&M, department stores, mall
                    - Electronics & Gadgets: phone, laptop, headphones, charger, cables, Apple, Samsung, OnePlus, repair, screen replacement, tech accessories

                    Education & Kids:
                    - Education: tuition, school fees, college fees, books, stationery, coaching, Udemy, Coursera, exam fees, uniforms, online courses
                    - Kids & Childcare: daycare, babysitter, toys, diapers, baby food, school supplies, kids clothes, playground, nursery fees

                    Entertainment & Subscriptions:
                    - Entertainment: movies (PVR, INOX, AMC), concerts, events, amusement parks, gaming, arcade, zoo, museum, sports match, bowling
                    - Subscriptions: Netflix, Spotify, Disney+, Hotstar, YouTube Premium, Apple One, LinkedIn, iCloud, Google One, Xbox Game Pass, Amazon Prime, Audible

                    Finance:
                    - Insurance: life insurance, health insurance, vehicle insurance, LIC, home insurance, travel insurance, term plan, premium payment
                    - Investments & Savings: SIP, mutual fund, stocks, FD, RD, PPF, NPS, crypto, trading (Zerodha, Groww, Robinhood, Coinbase, Binance)
                    - EMI & Loan: home loan EMI, car loan, personal loan, credit card payment, BNPL, Afterpay, Klarna, Bajaj Finance
                    - Taxes & Fees: income tax, property tax, GST, council tax, government fees, passport, driving license, RTO, registration fee

                    Social & Giving:
                    - Gifts & Occasions: birthday gifts, wedding gift, anniversary, festival spending (Diwali, Christmas, Eid, Holi), flowers, greeting cards, celebration
                    - Charity & Donations: NGO donation, temple/church/mosque offering, crowdfunding, zakat, tithe, relief funds, charity

                    Work:
                    - Business & Work Expenses: office supplies, client meals, co-working space, work travel, courier, printing, SaaS tools (Slack, Notion, Figma, Zoom, AWS)
                    - Personal Services: laundry, dry cleaning, tailor, cobbler, domestic help salary, maid, cook, watchman, driver salary, ironing

                    - General: LAST RESORT ONLY — use when truly no other category fits at all

                    VENDOR RULES — be smart, never literal:
                    - Named brand/shop/app → use exactly that name
                    - Rent/housing → 'Landlord' or person/company name if given
                    - EMI/loan → bank name (e.g. 'HDFC Bank', 'SBI', 'Barclays')
                    - Utility bill → provider name if mentioned, else 'Electricity Board', 'Water Board'
                    - Domestic help/maid/driver salary → use their name if given, else 'Domestic help'
                    - Person-to-person → use the person's name
                    - Street food / unnamed local → 'Street stall' or 'Local shop'
                    - Government payment → 'Government' or department name
                    - Use 'Unknown' ONLY if there is truly zero vendor information

                    AMOUNT:
                    - Numeric value only, no currency symbols
                    - Handle: '$20', '€15.50', '£8', '¥500', '₹200', 'Rs 50', '20 dollars', 'twenty euros', '15,99' (European comma)
                    - Convert word numbers: 'twenty thousand' → 20000, 'five hundred' → 500
                    - Multiple amounts → pick the final/total

                    CURRENCY — ISO 4217 code:
                    - $ → USD (or AUD/CAD/SGD if context is clear), € → EUR, £ → GBP, ¥ → JPY, ₹ → INR
                    - 'rupees' → INR, 'dollars' → USD, 'euros' → EUR, 'pounds' → GBP, 'dirhams' → AED, 'ringgit' → MYR, 'baht' → THB, 'won' → KRW, 'yuan/RMB' → CNY
                    - Default: 'USD' if truly unclear

                    DATE:
                    - Resolve relative dates ('yesterday', 'last Monday', '2 days ago') from today's date below
                    - Handle absolute formats: '5th May', '03/15', 'March 15', '15-03'
                    - Default: today if not mentioned

                    Return ONLY valid JSON, no explanation, no markdown:
                    {""vendor"": ""..."", ""amount"": 0.00, ""currency"": ""USD"", ""category"": ""..."", ""date"": ""yyyy-MM-dd"", ""parsed"": true, ""rawText"": ""...""}",
                   input = $"Parse this expense: {text}\nToday is {DateTime.UtcNow:yyyy-MM-dd}"
                };

                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return TryParseTextLocally(text);

                var json = await response.Content.ReadAsStringAsync();
                var rawText = ExtractResponseText(json);
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
                    Vendor = root.TryGetProperty("vendor", out var v) ? v.GetString() ?? "Unknown" : "Unknown",
                    Amount = root.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0,
                    Category = root.TryGetProperty("category", out var c) ? c.GetString() ?? "General" : "General",
                    Date = root.TryGetProperty("date", out var d) ? d.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
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
            var amountMatch = System.Text.RegularExpressions.Regex.Match(text, @"\$?([\d,]+\.?\d*)");
            var amount = amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", ""), out var parsedAmount)
                ? parsedAmount : 0m;

            return new ParseTextResultDto
            {
                Vendor = "Unknown",
                Amount = amount,
                Category = "General",
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Parsed = amount > 0,
                RawText = text
            };
        }

        public async Task<VendorAnalysisDto> GetVendorAnalysisAsync(int userId)
        {
            var cacheKey = GetUserCacheKey("vendor-analysis", userId);
            if (_cache.TryGetValue(cacheKey, out VendorAnalysisDto? cachedAnalysis) && cachedAnalysis != null)
            {
                return cachedAnalysis;
            }

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var priorMonthStart = monthStart.AddMonths(-1);

            var receipts = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.UploadedAt >= priorMonthStart)
                .ToListAsync();

            var thisMonth = receipts.Where(r => r.UploadedAt >= monthStart).ToList();
            var priorMonth = receipts.Where(r => r.UploadedAt < monthStart).ToList();

            var priorByVendor = priorMonth
                .Where(r => !string.IsNullOrWhiteSpace(r.Vendor))
                .GroupBy(r => r.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount), StringComparer.OrdinalIgnoreCase);

            var vendors = thisMonth
                .Where(r => !string.IsNullOrWhiteSpace(r.Vendor))
                .GroupBy(r => r.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var total = g.Sum(r => r.TotalAmount);
                    var count = g.Count();
                    priorByVendor.TryGetValue(g.Key, out var prior);
                    decimal? changePct = prior > 0 ? ((total - prior) / prior) * 100m : null;
                    var trend = prior <= 0 ? "new" : changePct >= 20 ? "up" : changePct <= -20 ? "down" : "steady";
                    return new VendorSummaryDto
                    {
                        Vendor = g.Key,
                        TotalSpend = Math.Round(total, 2),
                        VisitCount = count,
                        AverageTransaction = Math.Round(total / count, 2),
                        ChangePercent = changePct.HasValue ? Math.Round(changePct.Value, 1) : null,
                        Trend = trend
                    };
                })
                .OrderByDescending(v => v.TotalSpend)
                .Take(10)
                .ToList();

            var topVendor = vendors.FirstOrDefault();
            var newVendors = vendors.Count(v => v.Trend == "new");
            var fallback = topVendor == null
                ? "No vendor data available yet. Upload receipts to see vendor analysis."
                : $"Your top vendor is {topVendor.Vendor} at {topVendor.TotalSpend:C} this month. {(newVendors > 0 ? $"{newVendors} new vendors appeared this month." : "Vendor mix is consistent with last month.")}";

            var snapshot = await GetInsightsAsync(userId);
            var narrative = await GetCachedModelReplyAsync(
                $"{GetUserCacheKey("vendor-analysis-narrative", userId)}:{now:yyyyMM}:{topVendor?.Vendor ?? "none"}:{topVendor?.TotalSpend ?? 0m}:{newVendors}",
                $"Vendor analysis for {now:MMMM}: top vendor is {topVendor?.Vendor ?? "none"} ({topVendor?.TotalSpend:C}). {newVendors} new vendors. Give one sentence of vendor spending observation.",
                snapshot,
                fallback);

            var analysis = new VendorAnalysisDto
            {
                Month = now.ToString("MMMM yyyy"),
                TopVendors = vendors,
                AiObservation = narrative
            };

            _cache.Set(cacheKey, analysis, TimeSpan.FromSeconds(30));
            return analysis;
        }

        public async Task<DuplicateCheckResultDto> CheckDuplicateReceiptAsync(int userId, string vendor, decimal amount, string date)
        {
            if (!DateTime.TryParse(date, out var parsedDate))
                parsedDate = DateTime.UtcNow;

            var windowStart = parsedDate.AddDays(-7);
            var windowEnd = parsedDate.AddDays(1);

            var candidates = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(r => r.UserId == userId
                    && r.UploadedAt >= windowStart
                    && r.UploadedAt <= windowEnd)
                .ToListAsync();

            var matches = candidates
                .Where(r =>
                    string.Equals(r.Vendor?.Trim(), vendor?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(r.TotalAmount - amount) < 0.01m)
                .Select(r => new ReceiptMatchDto
                {
                    Id = r.Id,
                    Vendor = r.Vendor ?? "Unknown",
                    Amount = r.TotalAmount,
                    Date = r.UploadedAt.ToString("yyyy-MM-dd"),
                    MatchReason = "Same vendor and amount within 7 days"
                })
                .ToList();

            if (matches.Count > 0)
            {
                return new DuplicateCheckResultDto
                {
                    IsDuplicate = true,
                    Warning = $"A receipt from {vendor} for {amount:C} was already uploaded within the last 7 days.",
                    PotentialMatches = matches
                };
            }

            return new DuplicateCheckResultDto
            {
                IsDuplicate = false,
                Warning = string.Empty,
                PotentialMatches = new List<ReceiptMatchDto>()
            };
        }

        private sealed record ForecastComputation(
            DateTime ReferenceUtc,
            decimal CurrentSpend,
            decimal ProjectedMonthEnd,
            decimal DailyAverage,
            int DaysElapsed,
            int DaysRemaining,
            string Trend,
            decimal TotalBudget,
            string TopCategory,
            List<ForecastDriverDto> Drivers,
            List<DailySpendPointDto> DailyBreakdown,
            AiInsightSnapshotDto Snapshot,
            string FallbackNarrative,
            BudgetAdvisorSnapshotDto BudgetAdvisor);

        private sealed record NotificationPreferenceSnapshot(
            bool BudgetNotificationsEnabled,
            bool AnomalyNotificationsEnabled,
            bool SubscriptionNotificationsEnabled);
    }
}
