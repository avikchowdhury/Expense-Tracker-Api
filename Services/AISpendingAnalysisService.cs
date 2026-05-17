using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExpenseTracker.Api.Services;

public sealed class AISpendingAnalysisService : IAISpendingAnalysisService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBudgetAdvisorService _budgetAdvisorService;
    private readonly IAIModelClient _aiModelClient;
    private readonly IAIInsightsService _aiInsightsService;
    private readonly IMemoryCache _cache;

    public AISpendingAnalysisService(
        IUnitOfWork unitOfWork,
        IBudgetAdvisorService budgetAdvisorService,
        IAIModelClient aiModelClient,
        IAIInsightsService aiInsightsService,
        IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _budgetAdvisorService = budgetAdvisorService;
        _aiModelClient = aiModelClient;
        _aiInsightsService = aiInsightsService;
        _cache = cache;
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
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= threeMonthsBack)
            .ToListAsync();

        var thisMonthByCategory = receipts
            .Where(receipt => receipt.UploadedAt >= monthStart && !string.IsNullOrWhiteSpace(receipt.Category))
            .GroupBy(receipt => receipt.Category!)
            .ToDictionary(group => group.Key, group => group.Sum(receipt => receipt.TotalAmount));

        var priorByCategory = receipts
            .Where(receipt => receipt.UploadedAt < monthStart && !string.IsNullOrWhiteSpace(receipt.Category))
            .GroupBy(receipt => receipt.Category!)
            .ToDictionary(group => group.Key, group => group.Sum(receipt => receipt.TotalAmount) / 3m);

        var anomalies = new List<SpendingAnomalyDto>();

        foreach (var entry in thisMonthByCategory)
        {
            if (!priorByCategory.TryGetValue(entry.Key, out var averageMonth) || averageMonth <= 0)
            {
                continue;
            }

            var percentageIncrease = ((entry.Value - averageMonth) / averageMonth) * 100m;
            if (percentageIncrease < 20)
            {
                continue;
            }

            anomalies.Add(new SpendingAnomalyDto
            {
                Category = entry.Key,
                ThisMonth = RoundCurrency(entry.Value),
                AverageMonth = RoundCurrency(averageMonth),
                PercentageIncrease = Math.Round(percentageIncrease, 1),
                Severity = percentageIncrease >= 100 ? ApplicationText.Severity.Critical : percentageIncrease >= 50 ? ApplicationText.Severity.Warning : "normal",
                Message = $"{entry.Key} is up {percentageIncrease:F0}% vs. your 3-month average ({averageMonth:C0} to {entry.Value:C0})."
            });
        }

        var results = anomalies.OrderByDescending(anomaly => anomaly.PercentageIncrease).Take(5).ToList();
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
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= monthStart)
            .ToListAsync();

        var totalSpend = receipts.Sum(receipt => receipt.TotalAmount);
        var topCategory = receipts
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt.Category))
            .GroupBy(receipt => receipt.Category!)
            .OrderByDescending(group => group.Sum(receipt => receipt.TotalAmount))
            .FirstOrDefault()?.Key ?? ApplicationText.Defaults.NotAvailable;

        var anomalies = await GetSpendingAnomaliesAsync(userId);
        var aiSummary = anomalies.Count > 0
            ? $"This month you've spent {totalSpend:C0} with {receipts.Count} receipts. Top category: {topCategory}. Notable: {anomalies.First().Message}"
            : $"This month you've spent {totalSpend:C0} across {receipts.Count} receipts. Top category: {topCategory}. Spending looks steady with no major anomalies.";

        var summary = new MonthlySummaryDto
        {
            Month = now.ToString("MMMM yyyy"),
            TotalSpend = RoundCurrency(totalSpend),
            TopCategory = topCategory,
            ReceiptCount = receipts.Count,
            AiSummary = aiSummary,
            Anomalies = anomalies
        };

        _cache.Set(cacheKey, summary, TimeSpan.FromSeconds(20));
        return summary;
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
            .Where(adjustment => !string.IsNullOrWhiteSpace(adjustment.Category) && adjustment.DeltaAmount != 0)
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
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= weekStart && receipt.UploadedAt < weekEnd)
            .ToListAsync();

        var totalSpend = receipts.Sum(receipt => receipt.TotalAmount);
        var topCategories = receipts
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt.Category))
            .GroupBy(receipt => receipt.Category!)
            .Select(group => new WeeklyCategorySpendDto
            {
                Category = group.Key,
                TotalSpend = RoundCurrency(group.Sum(receipt => receipt.TotalAmount))
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
            TopCategory = topCategories.FirstOrDefault()?.Category ?? ApplicationText.Defaults.NotAvailable,
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
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= priorMonthStart)
            .ToListAsync();

        var thisMonth = receipts.Where(receipt => receipt.UploadedAt >= monthStart).ToList();
        var priorMonth = receipts.Where(receipt => receipt.UploadedAt < monthStart).ToList();

        var priorByVendor = priorMonth
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt.Vendor))
            .GroupBy(receipt => receipt.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(receipt => receipt.TotalAmount), StringComparer.OrdinalIgnoreCase);

        var vendors = thisMonth
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt.Vendor))
            .GroupBy(receipt => receipt.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var total = group.Sum(receipt => receipt.TotalAmount);
                var count = group.Count();
                priorByVendor.TryGetValue(group.Key, out var prior);
                decimal? changePercent = prior > 0 ? ((total - prior) / prior) * 100m : null;
                var trend = prior <= 0 ? "new" : changePercent >= 20 ? "up" : changePercent <= -20 ? "down" : "steady";

                return new VendorSummaryDto
                {
                    Vendor = group.Key,
                    TotalSpend = RoundCurrency(total),
                    VisitCount = count,
                    AverageTransaction = RoundCurrency(total / count),
                    ChangePercent = changePercent.HasValue ? Math.Round(changePercent.Value, 1) : null,
                    Trend = trend
                };
            })
            .OrderByDescending(vendor => vendor.TotalSpend)
            .Take(10)
            .ToList();

        var topVendor = vendors.FirstOrDefault();
        var newVendors = vendors.Count(vendor => vendor.Trend == "new");
        var fallback = topVendor == null
            ? "No vendor data available yet. Upload receipts to see vendor analysis."
            : $"Your top vendor is {topVendor.Vendor} at {topVendor.TotalSpend:C} this month. {(newVendors > 0 ? $"{newVendors} new vendors appeared this month." : "Vendor mix is consistent with last month.")}";

        var snapshot = await _aiInsightsService.GetInsightsAsync(userId);
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
        {
            parsedDate = DateTime.UtcNow;
        }

        var windowStart = parsedDate.AddDays(-7);
        var windowEnd = parsedDate.AddDays(1);

        var candidates = await _unitOfWork.Receipts.Query()
            .AsNoTracking()
            .Where(receipt =>
                receipt.UserId == userId &&
                receipt.UploadedAt >= windowStart &&
                receipt.UploadedAt <= windowEnd)
            .ToListAsync();

        var matches = candidates
            .Where(receipt =>
                string.Equals(receipt.Vendor?.Trim(), vendor?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(receipt.TotalAmount - amount) < 0.01m)
            .Select(receipt => new ReceiptMatchDto
            {
                Id = receipt.Id,
                Vendor = receipt.Vendor ?? ApplicationText.Defaults.UnknownVendor,
                Amount = receipt.TotalAmount,
                Date = receipt.UploadedAt.ToString("yyyy-MM-dd"),
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

    private async Task<ForecastComputation> BuildForecastAsync(int userId, DateTime referenceUtc)
    {
        var monthStart = new DateTime(referenceUtc.Year, referenceUtc.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(referenceUtc.Year, referenceUtc.Month);
        var daysElapsed = Math.Max(1, (referenceUtc - monthStart).Days + 1);
        var daysRemaining = Math.Max(daysInMonth - daysElapsed, 0);

        var dailySpending = await _unitOfWork.Receipts.Query()
            .AsNoTracking()
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= monthStart && receipt.UploadedAt < monthEnd)
            .GroupBy(receipt => receipt.UploadedAt.Date)
            .Select(group => new
            {
                Date = group.Key,
                Amount = group.Sum(item => item.TotalAmount)
            })
            .OrderBy(item => item.Date)
            .ToListAsync();

        var drivers = await _unitOfWork.Receipts.Query()
            .AsNoTracking()
            .Where(receipt =>
                receipt.UserId == userId &&
                receipt.UploadedAt >= monthStart &&
                receipt.UploadedAt < monthEnd &&
                !string.IsNullOrWhiteSpace(receipt.Category))
            .GroupBy(receipt => receipt.Category!)
            .Select(group => new ForecastDriverDto
            {
                Category = group.Key,
                Amount = RoundCurrency(group.Sum(item => item.TotalAmount))
            })
            .OrderByDescending(item => item.Amount)
            .Take(3)
            .ToListAsync();

        var recentMonthlyTotals = await _unitOfWork.Receipts.Query()
            .AsNoTracking()
            .Where(receipt => receipt.UserId == userId && receipt.UploadedAt >= referenceUtc.AddMonths(-3))
            .GroupBy(receipt => new { receipt.UploadedAt.Year, receipt.UploadedAt.Month })
            .Select(group => group.Sum(item => item.TotalAmount))
            .ToListAsync();

        var currentSpend = dailySpending.Sum(item => item.Amount);
        var dailyAverage = daysElapsed > 0 ? currentSpend / daysElapsed : 0m;
        var projectedMonthEnd = currentSpend + (dailyAverage * daysRemaining);
        var budgetAdvisor = await _budgetAdvisorService.GetBudgetAdvisorAsync(userId, referenceUtc);
        var trend = GetForecastTrend(projectedMonthEnd, budgetAdvisor.TotalBudget);
        var topCategory = drivers.FirstOrDefault()?.Category ?? ApplicationText.Defaults.NotAvailable;
        var fallbackNarrative = trend == ApplicationText.Severity.Critical
            ? $"You're on pace to spend {projectedMonthEnd:C} this month. That exceeds your budget and deserves an immediate cutback plan."
            : trend == ApplicationText.Severity.Warning
                ? $"Projected month-end spend of {projectedMonthEnd:C} is approaching your limit. Watch your top categories."
                : $"Spending looks controlled at {dailyAverage:C}/day. Projected month-end total: {projectedMonthEnd:C}.";

        var snapshot = new AiInsightSnapshotDto
        {
            GeneratedAt = referenceUtc,
            BudgetHealth = trend == ApplicationText.Severity.Critical
                ? "Over budget"
                : trend == ApplicationText.Severity.Warning
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

        var dailyByDate = dailySpending.ToDictionary(item => item.Date, item => RoundCurrency(item.Amount));
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
            return ApplicationText.Severity.Critical;
        }

        return projectedMonthEnd >= totalBudget * 0.8m
            ? ApplicationText.Severity.Warning
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

        var reply = await _aiModelClient.GenerateGroundedReplyAsync(userMessage, snapshot, fallbackReply);
        _cache.Set(cacheKey, reply, TimeSpan.FromMinutes(5));
        return reply;
    }

    private static string GetUserCacheKey(string scope, int userId) => $"ai:{scope}:{userId}";

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
}
