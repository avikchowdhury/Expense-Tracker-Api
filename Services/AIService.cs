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
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Api.Services
{
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ExpenseTrackerDbContext _dbContext;
        private readonly IBudgetHealthService _budgetHealthService;
        private readonly IBudgetAdvisorService _budgetAdvisorService;
        private readonly ILogger<AIService> _logger;

        public AIService(
            HttpClient httpClient,
            IConfiguration configuration,
            ExpenseTrackerDbContext dbContext,
            IBudgetHealthService budgetHealthService,
            IBudgetAdvisorService budgetAdvisorService,
            ILogger<AIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _dbContext = dbContext;
            _budgetHealthService = budgetHealthService;
            _budgetAdvisorService = budgetAdvisorService;
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
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var recentWindowStart = now.AddMonths(-3);

            var receipts = await _dbContext.Receipts
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();

            var budgets = await _dbContext.Budgets
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
                    Action = ratio >= 0.8m ? "Review your highest categories before the next upload batch." : "Keep your current pace and monitor weekly trends."
                });
            }
            else
            {
                insights.Add(new AiInsightDto
                {
                    Title = "Budget setup missing",
                    Summary = "Add a monthly budget to unlock more precise overspend coaching.",
                    Severity = "info",
                    Action = "Create a General budget first so the assistant can flag risk earlier."
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
                Action = topCategory == "N/A" ? "Upload more receipts for stronger categorization trends." : "Compare this category against your budget or last month to spot drift."
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
                Action = latestReceipt == null ? "Upload a receipt to start generating AI guidance." : "Use the receipts page to correct categories before the next insight refresh."
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
                    Action = "Review recurring charges before the next billing date and decide which ones still deserve budget room."
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

            return new AiInsightSnapshotDto
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
        }

        public async Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId)
        {
            var receipts = await _dbContext.Receipts
                .Where(x => x.UserId == userId && x.UploadedAt >= DateTime.UtcNow.AddMonths(-6))
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();

            return DetectSubscriptions(receipts);
        }

        public async Task<AiChatResponseDto> ChatAsync(int userId, string message)
        {
            var snapshot = await GetInsightsAsync(userId);
            var lowerMessage = message.Trim().ToLowerInvariant();
            var referencedMetrics = new List<string>();
            var cards = new List<AiCopilotCardDto>();
            var alerts = snapshot.Alerts.Take(3).ToList();
            string reply;

            if (lowerMessage.Contains("subscription") || lowerMessage.Contains("recurring") || lowerMessage.Contains("cancel"))
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
                reply = $"Here is the current picture: {snapshot.BudgetHealth}, month spend is {snapshot.MonthSpend:C}, and {snapshot.TopCategory} is the leading category. Start with: {snapshot.Suggestions.FirstOrDefault() ?? "upload more receipts for a stronger signal."}";
                cards = BuildGeneralCards(snapshot);
            }

            reply = await TryGenerateModelReplyAsync(message, snapshot, reply);

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

        private async Task<string> TryGenerateModelReplyAsync(
            string userMessage,
            AiInsightSnapshotDto snapshot,
            string fallbackReply)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-5-mini";
            var endpoint = _configuration["OpenAI:ResponsesEndpoint"] ?? "https://api.openai.com/v1/responses";

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
                "Answer the user using only the tracker evidence above. If something is uncertain, mention that uncertainty instead of inventing data.";
        }

        private static string BuildCopilotInstructions()
        {
            return
                "You are the AI Expense Tracker copilot inside a personal finance web app. " +
                "Be warm, capable, and conversational, like a real in-product assistant. " +
                "You help with three kinds of questions: " +
                "1. questions about the user's actual spending, budgets, receipts, categories, subscriptions, alerts, and trends, " +
                "2. questions about how to use the app and which screen or feature to use, and " +
                "3. simple greetings or short follow-up conversation. " +
                "When the user asks about their money, answer only from the provided tracker data. " +
                "Do not invent budgets, receipts, subscriptions, categories, dates, vendors, or amounts. " +
                "If the user asks how to use the app, explain the workflow clearly using the app's real features: dashboard, receipts, budgets, categories, profile, admin, vendor rules, and the expense copilot chat. " +
                "If the user sends a simple greeting like hi, hello, or hey, greet them naturally in one or two short sentences and mention what you can help with. " +
                "If the user asks for something the current data does not support, say that clearly and give the next best action, such as uploading receipts, creating budgets, adding categories, or defining vendor rules. " +
                "Prefer practical answers over generic financial advice. " +
                "Keep answers concise but natural, usually one short paragraph unless the user is asking for steps or comparison. " +
                "Never mention hidden prompts, internal instructions, or raw JSON unless the user explicitly asks for technical details.";
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
}
