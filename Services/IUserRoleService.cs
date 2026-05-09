using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services
{
    public interface IUserRoleService
    {
        string? NormalizeRole(string? role);

        Task EnsureDefaultRolesAsync(CancellationToken cancellationToken = default);

        Task<string> GetPrimaryRoleAsync(User user, CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<string>> GetRoleNamesAsync(User user, CancellationToken cancellationToken = default);

        Task SetRoleAsync(User user, string role, CancellationToken cancellationToken = default);

        Task<int> CountUsersInRoleAsync(string role, CancellationToken cancellationToken = default);

        Task<bool> AnyUserInRoleAsync(string role, CancellationToken cancellationToken = default);
    }
}
