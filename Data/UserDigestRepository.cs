using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data
{
    public sealed class UserDigestRepository : IUserDigestRepository
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public UserDigestRepository(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<UserDigestTarget?> GetUserDigestTargetAsync(int userId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .AsNoTracking()
                .Where(candidate => candidate.Id == userId)
                .Select(MapTarget())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyCollection<UserDigestTarget>> ListUserDigestTargetsAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Select(MapTarget())
                .ToListAsync(cancellationToken);
        }

        private static Expression<Func<Models.User, UserDigestTarget>> MapTarget()
        {
            return candidate => new UserDigestTarget(
                candidate.Id,
                candidate.Email,
                candidate.FullName,
                candidate.WeeklySummaryDay,
                candidate.WeeklySummaryEmailEnabled,
                candidate.MonthlyReportEmailEnabled);
        }
    }
}
