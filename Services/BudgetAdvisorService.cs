using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public class BudgetAdvisorService : IBudgetAdvisorService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BudgetAdvisorService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<BudgetAdvisorSnapshotDto> GetBudgetAdvisorAsync(int userId, DateTime? referenceUtc = null)
        {
            var now = referenceUtc ?? DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var previousMonths = Enumerable.Range(1, 3)
                .Select(offset => monthStart.AddMonths(-offset))
                .OrderBy(date => date)
                .ToList();
            var historyStart = previousMonths.First();

            var budgets = await _unitOfWork.Budgets.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new BudgetReadModel(x.Category, x.MonthlyLimit))
                .ToListAsync();

            var expenses = await _unitOfWork.Expenses.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Date >= historyStart && x.Date < monthEnd)
                .Select(x => new ExpenseReadModel(
                    x.Date,
                    x.Amount,
                    x.Category != null && x.Category.Name != null && x.Category.Name != string.Empty
                        ? x.Category.Name
                        : "Uncategorized"))
                .ToListAsync();

            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var daysElapsed = Math.Clamp(now.Day, 1, daysInMonth);
            var daysRemaining = Math.Max(daysInMonth - daysElapsed, 0);

            var budgetByCategory = budgets
                .GroupBy(x => NormalizeCategoryKey(x.Category))
                .ToDictionary(
                    group => group.Key,
                    group => new CategoryBudgetAggregate(
                        PickDisplayName(group.Select(x => x.Category), "General"),
                        RoundCurrency(group.Sum(item => item.MonthlyLimit))),
                    StringComparer.OrdinalIgnoreCase);

            var knownExpenseCategoryNames = expenses
                .GroupBy(x => NormalizeCategoryKey(x.CategoryName))
                .ToDictionary(
                    group => group.Key,
                    group => PickDisplayName(group.Select(x => x.CategoryName), "Uncategorized"),
                    StringComparer.OrdinalIgnoreCase);

            var currentMonthExpenses = expenses
                .Where(x => x.Date >= monthStart)
                .ToList();

            var currentSpendByCategory = currentMonthExpenses
                .GroupBy(x => NormalizeCategoryKey(x.CategoryName))
                .ToDictionary(
                    group => group.Key,
                    group => new CategorySpendAggregate(
                        PickDisplayName(group.Select(x => x.CategoryName), "Uncategorized"),
                        RoundCurrency(group.Sum(item => item.Amount))),
                    StringComparer.OrdinalIgnoreCase);

            var historicalMonthlySpend = expenses
                .Where(x => x.Date < monthStart)
                .GroupBy(x => new
                {
                    CategoryKey = NormalizeCategoryKey(x.CategoryName),
                    Month = new DateTime(x.Date.Year, x.Date.Month, 1)
                })
                .ToDictionary(
                    group => (group.Key.CategoryKey, group.Key.Month),
                    group => RoundCurrency(group.Sum(item => item.Amount)));

            var categoryKeys = budgetByCategory.Keys
                .Union(currentSpendByCategory.Keys, StringComparer.OrdinalIgnoreCase)
                .Union(historicalMonthlySpend.Keys.Select(key => key.CategoryKey), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categorySnapshots = categoryKeys
                .Select(key =>
                {
                    budgetByCategory.TryGetValue(key, out var budgetAggregate);
                    currentSpendByCategory.TryGetValue(key, out var spendAggregate);

                    var budget = budgetAggregate?.Amount ?? 0m;
                    var spent = spendAggregate?.Amount ?? 0m;
                    var projectedSpend = daysElapsed > 0
                        ? RoundCurrency((spent / daysElapsed) * daysInMonth)
                        : spent;
                    var historicalAverage = RoundCurrency(previousMonths.Average(month =>
                        historicalMonthlySpend.TryGetValue((key, month), out var total) ? total : 0m));
                    var suggestedBudget = CalculateSuggestedBudget(projectedSpend, historicalAverage, budget);
                    var remaining = RoundCurrency(budget - spent);
                    var categoryName = budgetAggregate?.Name
                        ?? spendAggregate?.Name
                        ?? (knownExpenseCategoryNames.TryGetValue(key, out var knownName)
                            ? knownName
                            : "Uncategorized");

                    var riskLevel = DetermineRiskLevel(budget, spent, projectedSpend);
                    var insight = BuildCategoryInsight(categoryName, budget, spent, projectedSpend, suggestedBudget, historicalAverage, remaining);

                    return new BudgetAdvisorCategoryDto
                    {
                        Category = categoryName,
                        Budget = budget,
                        Spent = spent,
                        ProjectedSpend = projectedSpend,
                        SuggestedBudget = suggestedBudget,
                        HistoricalAverage = historicalAverage,
                        Remaining = remaining,
                        RiskLevel = riskLevel,
                        Insight = insight
                    };
                })
                .OrderByDescending(item => GetRiskScore(item.RiskLevel))
                .ThenByDescending(item => Math.Max(item.ProjectedSpend - item.Budget, item.Spent))
                .ThenBy(item => item.Category)
                .ToList();

            var totalBudget = RoundCurrency(budgets.Sum(x => x.MonthlyLimit));
            var currentSpend = RoundCurrency(currentMonthExpenses.Sum(x => x.Amount));
            var projectedTotal = daysElapsed > 0
                ? RoundCurrency((currentSpend / daysElapsed) * daysInMonth)
                : currentSpend;
            var suggestedBudget = RoundCurrency(categorySnapshots.Sum(item => item.SuggestedBudget));
            var remainingBudget = RoundCurrency(totalBudget - currentSpend);
            var paceStatus = DetermineRiskLevel(totalBudget, currentSpend, projectedTotal);
            var summary = BuildSummary(totalBudget, currentSpend, projectedTotal, daysRemaining);
            var recommendations = BuildRecommendations(
                totalBudget,
                projectedTotal,
                suggestedBudget,
                daysRemaining,
                categorySnapshots);

            return new BudgetAdvisorSnapshotDto
            {
                GeneratedAt = now,
                TotalBudget = totalBudget,
                CurrentSpend = currentSpend,
                ProjectedSpend = projectedTotal,
                SuggestedBudget = suggestedBudget,
                RemainingBudget = remainingBudget,
                DaysElapsed = daysElapsed,
                DaysRemaining = daysRemaining,
                DaysInMonth = daysInMonth,
                PaceStatus = paceStatus,
                Summary = summary,
                Recommendations = recommendations,
                Categories = categorySnapshots
            };
        }

        private static decimal CalculateSuggestedBudget(decimal projectedSpend, decimal historicalAverage, decimal currentBudget)
        {
            var baseline = Math.Max(projectedSpend, historicalAverage);
            if (baseline <= 0m)
            {
                return RoundCurrency(currentBudget);
            }

            return RoundCurrency(baseline * 1.08m);
        }

        private static string BuildSummary(decimal totalBudget, decimal currentSpend, decimal projectedSpend, int daysRemaining)
        {
            if (totalBudget <= 0m && currentSpend <= 0m)
            {
                return "Add your first budget to unlock month-end pacing, category watchlists, and smarter coaching.";
            }

            if (totalBudget <= 0m)
            {
                return $"You have already spent {currentSpend:C} this month without a budget ceiling. Set category limits so the assistant can flag drift earlier.";
            }

            return $"At the current pace you are projected to finish the month at {projectedSpend:C} against a planned budget of {totalBudget:C}. {daysRemaining} day{(daysRemaining == 1 ? string.Empty : "s")} remain in this cycle.";
        }

        private static List<string> BuildRecommendations(
            decimal totalBudget,
            decimal projectedSpend,
            decimal suggestedBudget,
            int daysRemaining,
            IReadOnlyCollection<BudgetAdvisorCategoryDto> categories)
        {
            var recommendations = new List<string>();
            var uncoveredCategories = categories
                .Where(item => item.Budget <= 0m && item.Spent > 0m)
                .Select(item => item.Category)
                .Take(2)
                .ToList();
            var highRiskCategories = categories
                .Where(item => item.RiskLevel == "critical" || item.RiskLevel == "warning")
                .Select(item => item.Category)
                .Take(2)
                .ToList();

            if (totalBudget <= 0m)
            {
                recommendations.Add("Start by creating budget caps for your two highest-spend categories so the assistant can measure burn rate correctly.");
            }

            if (uncoveredCategories.Count > 0)
            {
                recommendations.Add($"Add budget cover for {JoinLabels(uncoveredCategories)} so active spending is not flying under the radar.");
            }

            if (highRiskCategories.Count > 0)
            {
                recommendations.Add($"Review {JoinLabels(highRiskCategories)} first. Those categories are creating the most month-end pressure right now.");
            }

            if (totalBudget > 0m && projectedSpend > totalBudget)
            {
                recommendations.Add($"At the current pace you are trending {projectedSpend - totalBudget:C} over plan. Trim discretionary spend or raise the categories that are chronically underfunded.");
            }
            else if (totalBudget > 0m && suggestedBudget > totalBudget * 1.1m)
            {
                recommendations.Add($"Recent pacing suggests a more realistic combined budget near {suggestedBudget:C}. Use that as your next planning baseline.");
            }
            else if (totalBudget > 0m && projectedSpend < totalBudget * 0.75m)
            {
                recommendations.Add("You still have healthy headroom this month. Consider tightening categories with excess slack so future alerts stay meaningful.");
            }

            if (daysRemaining <= 7 && totalBudget > 0m)
            {
                recommendations.Add($"Only {daysRemaining} day{(daysRemaining == 1 ? string.Empty : "s")} remain this month, so focus on the watchlist categories before adding new variable spend.");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Budget pacing looks stable. Keep categorizing receipts consistently so the assistant can refine next month's targets.");
            }

            return recommendations
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
        }

        private static string BuildCategoryInsight(
            string category,
            decimal budget,
            decimal spent,
            decimal projectedSpend,
            decimal suggestedBudget,
            decimal historicalAverage,
            decimal remaining)
        {
            if (budget <= 0m && spent <= 0m)
            {
                return "No budget or spend activity yet. This category will wake up once expenses start landing here.";
            }

            if (budget <= 0m)
            {
                return $"You have {spent:C} in tracked spend here but no budget cover yet. Start with a target near {suggestedBudget:C}.";
            }

            if (projectedSpend > budget)
            {
                return $"Projected to overshoot by {projectedSpend - budget:C}. This is the strongest candidate for an immediate adjustment.";
            }

            if (historicalAverage > budget)
            {
                return $"Your last three months averaged {historicalAverage:C}, which is already above the current cap.";
            }

            if (suggestedBudget < budget * 0.75m && spent > 0m)
            {
                return $"This budget has extra headroom. A tighter target near {suggestedBudget:C} would still cover recent pace.";
            }

            return $"Projected to finish at {projectedSpend:C}, leaving about {remaining:C} of headroom if this pace holds.";
        }

        private static string DetermineRiskLevel(decimal budget, decimal spent, decimal projectedSpend)
        {
            if (budget <= 0m)
            {
                return spent > 0m || projectedSpend > 0m ? "warning" : "info";
            }

            var projectedRatio = projectedSpend / budget;
            var currentRatio = spent / budget;

            if (projectedRatio >= 1m)
            {
                return "critical";
            }

            if (projectedRatio >= 0.85m || currentRatio >= 0.75m)
            {
                return "warning";
            }

            return "positive";
        }

        private static int GetRiskScore(string riskLevel) => riskLevel switch
        {
            "critical" => 3,
            "warning" => 2,
            "positive" => 1,
            _ => 0
        };

        private static string NormalizeCategoryKey(string? category) =>
            string.IsNullOrWhiteSpace(category) ? "uncategorized" : category.Trim().ToLowerInvariant();

        private static string PickDisplayName(IEnumerable<string?> names, string fallback)
        {
            return names
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?.Trim()
                ?? fallback;
        }

        private static string JoinLabels(IReadOnlyList<string> labels)
        {
            return labels.Count switch
            {
                0 => string.Empty,
                1 => labels[0],
                2 => $"{labels[0]} and {labels[1]}",
                _ => $"{string.Join(", ", labels.Take(labels.Count - 1))}, and {labels[^1]}"
            };
        }

        private static decimal RoundCurrency(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record BudgetReadModel(string Category, decimal MonthlyLimit);
        private sealed record ExpenseReadModel(DateTime Date, decimal Amount, string CategoryName);
        private sealed record CategoryBudgetAggregate(string Name, decimal Amount);
        private sealed record CategorySpendAggregate(string Name, decimal Amount);
    }
}
