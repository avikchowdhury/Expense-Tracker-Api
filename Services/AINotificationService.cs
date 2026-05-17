using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExpenseTracker.Api.Services;

public sealed class AINotificationService : IAINotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIInsightsService _aiInsightsService;
    private readonly IAISpendingAnalysisService _spendingAnalysisService;
    private readonly IMemoryCache _cache;

    public AINotificationService(
        IUnitOfWork unitOfWork,
        IAIInsightsService aiInsightsService,
        IAISpendingAnalysisService spendingAnalysisService,
        IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _aiInsightsService = aiInsightsService;
        _spendingAnalysisService = spendingAnalysisService;
        _cache = cache;
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

        var alerts = (await _aiInsightsService.GetInsightsAsync(userId)).Alerts;
        foreach (var alert in alerts.Take(5))
        {
            notifications.Add(new NotificationDto
            {
                Id = $"alert-{alert.Title.GetHashCode():x}",
                Title = alert.Title,
                Message = alert.Detail,
                Type = alert.Severity == ApplicationText.Severity.Critical || alert.Severity == ApplicationText.Severity.Warning ? "budget" : "info",
                Severity = alert.Severity,
                GeneratedAt = now
            });
        }

        var anomalies = await _spendingAnalysisService.GetSpendingAnomaliesAsync(userId);
        foreach (var anomaly in anomalies.Where(anomaly => anomaly.Severity != "normal").Take(3))
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

        var subscriptions = await _aiInsightsService.GetSubscriptionsAsync(userId);
        foreach (var subscription in subscriptions.Where(item => item.NextExpectedDate.HasValue && item.NextExpectedDate.Value <= now.AddDays(7)).Take(2))
        {
            notifications.Add(new NotificationDto
            {
                Id = $"sub-{subscription.Vendor.GetHashCode():x}",
                Title = $"Upcoming charge: {subscription.Vendor}",
                Message = $"Expected around {subscription.NextExpectedDate:MMM d} for approx. {subscription.AverageAmount:C}.",
                Type = "subscription",
                Severity = ApplicationText.Severity.Info,
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
            .OrderByDescending(notification => GetSeverityRank(notification.Severity))
            .ThenByDescending(notification => notification.GeneratedAt)
            .ToList();

        _cache.Set(cacheKey, results, TimeSpan.FromSeconds(15));
        return results;
    }

    private static string GetUserCacheKey(string scope, int userId) => $"ai:{scope}:{userId}";

    private static int GetSeverityRank(string severity) => severity switch
    {
        "critical" => 3,
        "warning" => 2,
        "positive" => 1,
        _ => 0
    };

    private sealed record NotificationPreferenceSnapshot(
        bool BudgetNotificationsEnabled,
        bool AnomalyNotificationsEnabled,
        bool SubscriptionNotificationsEnabled);
}
