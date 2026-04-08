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

namespace ExpenseTracker.Api.Services
{
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ExpenseTrackerDbContext _dbContext;
        private readonly IBudgetHealthService _budgetHealthService;

        public AIService(
            HttpClient httpClient,
            IConfiguration configuration,
            ExpenseTrackerDbContext dbContext,
            IBudgetHealthService budgetHealthService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _dbContext = dbContext;
            _budgetHealthService = budgetHealthService;
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
                MonthSpend = monthSpend,
                RecentAverage = recentAverage,
                TopCategory = topCategory,
                Anomalies = anomalies,
                Suggestions = suggestions.Distinct().Take(4).ToList(),
                Insights = insights
            };
        }

        public async Task<AiChatResponseDto> ChatAsync(int userId, string message)
        {
            var snapshot = await GetInsightsAsync(userId);
            var lowerMessage = message.Trim().ToLowerInvariant();
            var referencedMetrics = new List<string>();
            string reply;

            if (lowerMessage.Contains("budget") || lowerMessage.Contains("overspend") || lowerMessage.Contains("limit"))
            {
                referencedMetrics.Add("Budget health");
                referencedMetrics.Add("Month spend");
                reply = snapshot.BudgetHealth == "No active budget"
                    ? "You do not have an active budget yet, so the biggest improvement is creating a General budget first. Once that is in place I can warn you before you overspend."
                    : $"Your current budget status is '{snapshot.BudgetHealth}'. Month-to-date spend is {snapshot.MonthSpend:C}. Focus first on {snapshot.TopCategory} because it is contributing the most pressure right now.";
            }
            else if (lowerMessage.Contains("category") || lowerMessage.Contains("spend most"))
            {
                referencedMetrics.Add("Top category");
                reply = snapshot.TopCategory == "N/A"
                    ? "I do not have enough categorized receipts yet to name a dominant category. Upload a few more receipts or correct categories in the receipts page."
                    : $"{snapshot.TopCategory} is your strongest spending signal right now. Review that category first if you want the fastest impact on this month's total.";
            }
            else if (lowerMessage.Contains("receipt") || lowerMessage.Contains("vendor"))
            {
                referencedMetrics.Add("Recent uploads");
                reply = snapshot.Insights.FirstOrDefault(x => x.Title == "Receipt activity")?.Summary
                    ?? "I do not have recent receipt activity to summarize yet.";
            }
            else
            {
                referencedMetrics.Add("Budget health");
                referencedMetrics.Add("Top category");
                reply = $"Here is the current picture: {snapshot.BudgetHealth}, month spend is {snapshot.MonthSpend:C}, and {snapshot.TopCategory} is the leading category. Start with: {snapshot.Suggestions.FirstOrDefault() ?? "upload more receipts for a stronger signal."}";
            }

            return new AiChatResponseDto
            {
                Reply = reply,
                Suggestions = snapshot.Suggestions.Take(3).ToList(),
                ReferencedMetrics = referencedMetrics,
                GeneratedAt = DateTime.UtcNow
            };
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
}
