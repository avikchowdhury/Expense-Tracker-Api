using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data
{
    public sealed class UserRoleRepository : IUserRoleRepository
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public UserRoleRepository(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<bool> RoleExistsAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            return _dbContext.Roles.AnyAsync(role => role.NormalizedName == normalizedName, cancellationToken);
        }

        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            return _dbContext.Roles.AddAsync(role, cancellationToken).AsTask();
        }

        public async Task LoadRoleMappingsAsync(User user, CancellationToken cancellationToken = default)
        {
            if (_dbContext.Entry(user).Collection(existingUser => existingUser.RoleMappings).IsLoaded &&
                user.RoleMappings.All(mapping => mapping.Role is not null))
            {
                return;
            }

            await _dbContext.Entry(user)
                .Collection(existingUser => existingUser.RoleMappings)
                .Query()
                .Include(mapping => mapping.Role)
                .LoadAsync(cancellationToken);
        }

        public void RemoveRoleMappings(IEnumerable<UserRoleMapping> mappings)
        {
            _dbContext.UserRoleMappings.RemoveRange(mappings);
        }

        public Task<Role> GetRoleByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            return _dbContext.Roles.FirstAsync(
                existingRole => existingRole.NormalizedName == normalizedName,
                cancellationToken);
        }

        public async Task<int> CountUsersInRoleAsync(
            string normalizedRole,
            string normalizedName,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Users
                .Where(user => user.Role == normalizedRole)
                .Select(user => user.Id)
                .Union(
                    _dbContext.UserRoleMappings
                        .Where(mapping => mapping.Role.NormalizedName == normalizedName)
                        .Select(mapping => mapping.UserId))
                .Distinct()
                .CountAsync(cancellationToken);
        }
    }
}
