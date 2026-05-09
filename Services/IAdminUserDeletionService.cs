using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface IAdminUserDeletionService
    {
        Task<AdminDeleteUsersResultDto> DeleteUsersAsync(
            int actingUserId,
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default);
    }
}
