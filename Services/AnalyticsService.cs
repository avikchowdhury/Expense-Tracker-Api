using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public AnalyticsService(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<MonthlySpendingDto>> GetMonthlySpendingAsync(int userId, int months = 6)
        {
            var cutoff = DateTime.UtcNow.AddMonths(-months + 1);
            var grouped = await _dbContext.Expenses
                .Where(x => x.UserId == userId && x.Date >= cutoff)
                .GroupBy(x => new { x.Date.Year, x.Date.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(e => e.Amount)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            var result = grouped.Select(g => new MonthlySpendingDto
            {
                Month = $"{g.Year}-{g.Month:D2}",
                Total = g.Total
            }).ToList();

            return result;
        }

        public async Task<IEnumerable<(string Category, decimal Total)>> GetCategoryBreakdownAsync(int userId)
        {
            var results = await _dbContext.Expenses
                .Where(x => x.UserId == userId)
                .Include(x => x.Category)
                .GroupBy(x => x.Category)
                .Select(g => new { CategoryName = g.Key != null ? g.Key.Name : "Uncategorized", Total = g.Sum(e => e.Amount) })
                .ToListAsync();

            return results.Select(r => (r.CategoryName, r.Total));
        }
    }
}
