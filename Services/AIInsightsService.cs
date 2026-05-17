using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExpenseTracker.Api.Services;

public sealed class AIInsightsService : IAIInsightsService
{
    private readonly IAIModelClient _aiModelClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBudgetHealthService _budgetHealthService;
    private readonly IBudgetAdvisorService _budgetAdvisorService;
    private readonly IMemoryCache _cache;

    public AIInsightsService(
        IAIModelClient aiModelClient,
        IUnitOfWork unitOfWork,
        IBudgetHealthService budgetHealthService,
        IBudgetAdvisorService budgetAdvisorService,
        IMemoryCache cache)
    {
        _aiModelClient = aiModelClient;
        _unitOfWork = unitOfWork;
        _budgetHealthService = budgetHealthService;
        _budgetAdvisorService = budgetAdvisorService;
        _cache = cache;
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
            .Where(receipt => receipt.UserId == userId)
            .OrderByDescending(receipt => receipt.UploadedAt)
            .ToListAsync();

        var budgets = await _unitOfWork.Budgets.Query()
            .AsNoTracking()
            .Where(budget => budget.UserId == userId)
            .OrderByDescending(budget => budget.LastReset)
            .ToListAsync();

        var monthReceipts = receipts.Where(receipt => receipt.UploadedAt >= monthStart).ToList();
        var recentReceipts = receipts.Where(receipt => receipt.UploadedAt >= recentWindowStart).ToList();
        var monthSpend = monthReceipts.Sum(receipt => receipt.TotalAmount);

        var groupedRecentMonths = recentReceipts
            .GroupBy(receipt => new { receipt.UploadedAt.Year, receipt.UploadedAt.Month })
            .Select(group => group.Sum(item => item.TotalAmount))
            .ToList();

        var recentAverage = groupedRecentMonths.Count > 0 ? groupedRecentMonths.Average() : 0m;

        var topCategoryGroup = monthReceipts
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt.Category))
            .GroupBy(receipt => receipt.Category!)
            .Select(group => new { Category = group.Key, Total = group.Sum(item => item.TotalAmount) })
            .OrderByDescending(group => group.Total)
            .FirstOrDefault();

        var topCategory = topCategoryGroup?.Category ?? ApplicationText.Defaults.NotAvailable;
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
            monthReceipts.Count(receipt =>
                string.IsNullOrWhiteSpace(receipt.Category) ||
                receipt.Category.Equals(ApplicationText.Defaults.UncategorizedCategory, StringComparison.OrdinalIgnoreCase)),
            receipts);

        if (budgetSnapshot.BudgetCount > 0)
        {
            insights.Add(new AiInsightDto
            {
                Title = "Budget pulse",
                Summary = $"You have spent {monthSpend:C} against a combined {budgetSnapshot.Budget:C} budget this month.",
                Severity = ratio >= 1m ? ApplicationText.Severity.Critical : ratio >= 0.8m ? ApplicationText.Severity.Warning : ApplicationText.Severity.Positive,
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
                Severity = ApplicationText.Severity.Info,
                Action = "Create a General budget first so the assistant can flag risk earlier.",
                ActionLabel = "Create budget",
                ActionRoute = "/budgets"
            });
        }

        insights.Add(new AiInsightDto
        {
            Title = "Category focus",
            Summary = topCategory == ApplicationText.Defaults.NotAvailable
                ? "No dominant category yet because recent receipts are limited."
                : $"{topCategory} is your leading category in the current month.",
            Severity = topCategory == ApplicationText.Defaults.NotAvailable ? ApplicationText.Severity.Info : ApplicationText.Severity.Positive,
            MetricLabel = "Top category",
            MetricValue = topCategory,
            Action = topCategory == ApplicationText.Defaults.NotAvailable
                ? "Upload more receipts for stronger categorization trends."
                : "Compare this category against your budget or last month to spot drift.",
            ActionLabel = topCategory == ApplicationText.Defaults.NotAvailable ? "Add receipts" : "Try what-if forecast",
            ActionRoute = topCategory == ApplicationText.Defaults.NotAvailable ? "/receipts" : "/forecast"
        });

        var latestReceipt = receipts.FirstOrDefault();
        insights.Add(new AiInsightDto
        {
            Title = "Receipt activity",
            Summary = latestReceipt == null
                ? "No receipts have been uploaded yet."
                : $"Your latest receipt is from {latestReceipt.Vendor ?? "an unknown vendor"} for {latestReceipt.TotalAmount:C}.",
            Severity = latestReceipt == null ? ApplicationText.Severity.Info : ApplicationText.Severity.Positive,
            MetricLabel = "Recent uploads",
            MetricValue = receipts.Take(5).Count().ToString(),
            Action = latestReceipt == null
                ? "Upload a receipt to start generating AI guidance."
                : "Use the receipts page to correct categories before the next insight refresh.",
            ActionLabel = latestReceipt == null ? "Add receipt" : "Open cleanup",
            ActionRoute = "/receipts"
        });

        if (subscriptions.Count > 0)
        {
            var leadingSubscription = subscriptions[0];
            insights.Add(new AiInsightDto
            {
                Title = "Recurring spend watch",
                Summary = $"{subscriptions.Count} subscription-like merchants stand out. {leadingSubscription.Vendor} is the strongest recurring spend signal.",
                Severity = ApplicationText.Severity.Positive,
                MetricLabel = "Recurring monthly",
                MetricValue = $"{subscriptions.Sum(item => item.EstimatedMonthlyCost):C0}",
                Action = "Review recurring charges before the next billing date and decide which ones still deserve budget room.",
                ActionLabel = "Review subscriptions",
                ActionRoute = "/insights?tab=subscriptions"
            });
        }

        var highestReceipt = monthReceipts.OrderByDescending(receipt => receipt.TotalAmount).FirstOrDefault();
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
            suggestions.Add(topCategory == ApplicationText.Defaults.NotAvailable
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
                    .Where(alert => alert.Severity == ApplicationText.Severity.Warning || alert.Severity == ApplicationText.Severity.Critical)
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
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= DateTime.UtcNow.AddMonths(-6))
            .OrderByDescending(receipt => receipt.UploadedAt)
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
            reply = snapshot.TopCategory == ApplicationText.Defaults.NotAvailable
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
            reply = snapshot.Insights.FirstOrDefault(insight => insight.Title == "Receipt activity")?.Summary
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

        reply = await _aiModelClient.GenerateGroundedReplyAsync(normalizedMessage.UserQuestion, snapshot, reply);

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

    private static string GetUserCacheKey(string scope, int userId) => $"ai:{scope}:{userId}";

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

        if (budgetAdvisor.PaceStatus == ApplicationText.Severity.Critical)
        {
            alerts.Add(new AiCopilotAlertDto
            {
                Title = "Budget overspend projected",
                Detail = $"Current pacing points to {budgetAdvisor.ProjectedSpend:C} against a {budgetAdvisor.TotalBudget:C} budget.",
                Severity = ApplicationText.Severity.Critical
            });
        }
        else if (budgetAdvisor.PaceStatus == ApplicationText.Severity.Warning)
        {
            alerts.Add(new AiCopilotAlertDto
            {
                Title = "Budget getting tight",
                Detail = $"Projected spend is nearing the monthly limit with only {budgetAdvisor.RemainingBudget:C} of headroom left.",
                Severity = ApplicationText.Severity.Warning
            });
        }

        foreach (var category in budgetAdvisor.Categories
                     .Where(item => item.RiskLevel == ApplicationText.Severity.Critical || item.RiskLevel == ApplicationText.Severity.Warning)
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
                Severity = ApplicationText.Severity.Warning
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
                Severity = ApplicationText.Severity.Info
            });
        }

        if (receipts.Count >= 8 && subscriptions.Count == 0)
        {
            alerts.Add(new AiCopilotAlertDto
            {
                Title = "Recurring spend still learning",
                Detail = "Upload a few more monthly receipts from the same vendors and the copilot will start detecting subscriptions.",
                Severity = ApplicationText.Severity.Info
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
                Tone = budgetPulse?.Severity == ApplicationText.Severity.Critical ? "critical" : budgetPulse?.Severity == ApplicationText.Severity.Warning ? "warning" : "positive"
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
                Tone = snapshot.TopCategory == ApplicationText.Defaults.NotAvailable ? "default" : "warning"
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
                Tone = snapshot.TopCategory == ApplicationText.Defaults.NotAvailable ? "default" : "positive"
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
                Tone = snapshot.TopCategory == ApplicationText.Defaults.NotAvailable ? "default" : "positive"
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
            Value = alert.Severity.Equals(ApplicationText.Severity.Critical, StringComparison.OrdinalIgnoreCase) ? "High"
                : alert.Severity.Equals(ApplicationText.Severity.Warning, StringComparison.OrdinalIgnoreCase) ? "Watch" : "Info",
            Detail = alert.Detail,
            Tone = alert.Severity.Equals(ApplicationText.Severity.Critical, StringComparison.OrdinalIgnoreCase)
                ? "critical"
                : alert.Severity.Equals(ApplicationText.Severity.Warning, StringComparison.OrdinalIgnoreCase)
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
                Tone = snapshot.TopCategory == ApplicationText.Defaults.NotAvailable ? "default" : "positive"
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
}
