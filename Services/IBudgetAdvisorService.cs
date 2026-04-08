using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface IBudgetAdvisorService
    {
        Task<BudgetAdvisorSnapshotDto> GetBudgetAdvisorAsync(int userId, DateTime? referenceUtc = null);
    }
}
