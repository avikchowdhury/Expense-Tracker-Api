namespace ExpenseTracker.Api.Data
{
    public interface IUserDigestRepository
    {
        Task<UserDigestTarget?> GetUserDigestTargetAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<UserDigestTarget>> ListUserDigestTargetsAsync(CancellationToken cancellationToken = default);
    }

    public sealed record UserDigestTarget(
        int Id,
        string Email,
        string? FullName,
        string WeeklySummaryDay,
        bool WeeklySummaryEmailEnabled,
        bool MonthlyReportEmailEnabled);
}
