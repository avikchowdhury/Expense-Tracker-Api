using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public sealed class BudgetHealthSnapshot
    {
        public decimal Budget { get; init; }
        public decimal Spent { get; init; }
        public string Status { get; init; } = "ok";
        public string Message { get; init; } = string.Empty;
        public int BudgetCount { get; init; }
    }

    public class BudgetHealthService : IBudgetHealthService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BudgetHealthService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<BudgetHealthSnapshot> GetBudgetHealthAsync(
            int userId,
            DateTime periodStartUtc,
            DateTime periodEndUtc)
        {
            var budgetSummary = await _unitOfWork.Budgets.Query()
                .AsNoTracking()
                .Where(budget => budget.UserId == userId)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Count = group.Count(),
                    TotalBudget = group.Sum(budget => budget.MonthlyLimit)
                })
                .FirstOrDefaultAsync();

            if (budgetSummary == null)
            {
                return new BudgetHealthSnapshot
                {
                    Budget = 0m,
                    Spent = 0m,
                    Status = "ok",
                    Message = "No budget set.",
                    BudgetCount = 0
                };
            }

            var budgetCount = budgetSummary.Count;
            var totalBudget = budgetSummary.TotalBudget;
            var spent = await _unitOfWork.Expenses.Query()
                .AsNoTracking()
                .Where(expense =>
                    expense.UserId == userId &&
                    expense.Date >= periodStartUtc &&
                    expense.Date < periodEndUtc)
                .SumAsync(expense => (decimal?)expense.Amount) ?? 0m;

            if (totalBudget <= 0)
            {
                return new BudgetHealthSnapshot
                {
                    Budget = 0m,
                    Spent = spent,
                    Status = "warning",
                    Message = $"{budgetCount} budget rule{(budgetCount == 1 ? string.Empty : "s")} found, but the combined limit is zero.",
                    BudgetCount = budgetCount
                };
            }

            var ratio = spent / totalBudget;
            var prefix = budgetCount == 1
                ? "1 budget rule active."
                : $"{budgetCount} budget rules active.";

            var (status, message) = ratio switch
            {
                >= 1m => ("over", $"{prefix} You have exceeded your total planned budget."),
                >= 0.8m => ("warning", $"{prefix} You are close to your total budget limit."),
                _ => ("ok", $"{prefix} You are well within your total budget.")
            };

            return new BudgetHealthSnapshot
            {
                Budget = totalBudget,
                Spent = spent,
                Status = status,
                Message = message,
                BudgetCount = budgetCount
            };
        }
    }
}
