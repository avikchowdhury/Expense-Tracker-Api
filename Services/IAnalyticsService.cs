using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface IAnalyticsService
    {
        Task<IEnumerable<MonthlySpendingDto>> GetMonthlySpendingAsync(int userId, int months = 6);
        Task<IEnumerable<(string Category, decimal Total)>> GetCategoryBreakdownAsync(int userId);
    }
}
