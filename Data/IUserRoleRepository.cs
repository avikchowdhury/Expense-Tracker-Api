using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Data
{
    public interface IUserRoleRepository
    {
        Task<bool> RoleExistsAsync(string normalizedName, CancellationToken cancellationToken = default);
        Task AddRoleAsync(Role role, CancellationToken cancellationToken = default);
        Task LoadRoleMappingsAsync(User user, CancellationToken cancellationToken = default);
        void RemoveRoleMappings(IEnumerable<UserRoleMapping> mappings);
        Task<Role> GetRoleByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
        Task<int> CountUsersInRoleAsync(string normalizedRole, string normalizedName, CancellationToken cancellationToken = default);
    }
}
