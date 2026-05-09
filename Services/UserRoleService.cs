using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public sealed class UserRoleService : IUserRoleService
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public UserRoleService(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public string? NormalizeRole(string? role)
        {
            if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return AppRoles.Admin;
            }

            if (string.Equals(role, AppRoles.User, StringComparison.OrdinalIgnoreCase))
            {
                return AppRoles.User;
            }

            return null;
        }

        public async Task EnsureDefaultRolesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var roleName in AppRoles.All)
            {
                var normalizedName = roleName.ToUpperInvariant();
                var exists = await _dbContext.Roles
                    .AnyAsync(role => role.NormalizedName == normalizedName, cancellationToken);

                if (!exists)
                {
                    await _dbContext.Roles.AddAsync(new Role
                    {
                        Name = roleName,
                        NormalizedName = normalizedName
                    }, cancellationToken);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<string> GetPrimaryRoleAsync(User user, CancellationToken cancellationToken = default)
        {
            var roleNames = await GetRoleNamesAsync(user, cancellationToken);
            var primaryRole = roleNames.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase)
                ? AppRoles.Admin
                : roleNames.FirstOrDefault() ?? AppRoles.User;

            user.Role = primaryRole;
            return primaryRole;
        }

        public async Task<IReadOnlyCollection<string>> GetRoleNamesAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            await EnsureDefaultRolesAsync(cancellationToken);

            if (!_dbContext.Entry(user).Collection(existingUser => existingUser.RoleMappings).IsLoaded)
            {
                await _dbContext.Entry(user)
                    .Collection(existingUser => existingUser.RoleMappings)
                    .Query()
                    .Include(mapping => mapping.Role)
                    .LoadAsync(cancellationToken);
            }

            if (user.RoleMappings.Count == 0)
            {
                await BackfillPrimaryRoleMappingAsync(user, cancellationToken);
            }

            return user.RoleMappings
                .Select(mapping => NormalizeRole(mapping.Role.Name))
                .Where(roleName => roleName is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public async Task SetRoleAsync(User user, string role, CancellationToken cancellationToken = default)
        {
            var normalizedRole = NormalizeRole(role)
                ?? throw new ArgumentException("Role must be either Admin or User.", nameof(role));

            await EnsureDefaultRolesAsync(cancellationToken);

            await _dbContext.Entry(user)
                .Collection(existingUser => existingUser.RoleMappings)
                .LoadAsync(cancellationToken);

            if (user.RoleMappings.Count > 0)
            {
                _dbContext.UserRoleMappings.RemoveRange(user.RoleMappings);
                user.RoleMappings.Clear();
            }

            var roleEntity = await GetRoleEntityAsync(normalizedRole, cancellationToken);
            user.Role = normalizedRole;
            user.RoleMappings.Add(new UserRoleMapping
            {
                UserId = user.Id,
                RoleId = roleEntity.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        public async Task<int> CountUsersInRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            var normalizedRole = NormalizeRole(role)
                ?? throw new ArgumentException("Role must be either Admin or User.", nameof(role));
            var normalizedName = normalizedRole.ToUpperInvariant();

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

        public async Task<bool> AnyUserInRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            return await CountUsersInRoleAsync(role, cancellationToken) > 0;
        }

        private async Task BackfillPrimaryRoleMappingAsync(User user, CancellationToken cancellationToken)
        {
            var fallbackRole = NormalizeRole(user.Role) ?? AppRoles.User;
            var roleEntity = await GetRoleEntityAsync(fallbackRole, cancellationToken);

            user.Role = fallbackRole;
            user.RoleMappings.Add(new UserRoleMapping
            {
                UserId = user.Id,
                RoleId = roleEntity.Id,
                AssignedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _dbContext.Entry(user)
                .Collection(existingUser => existingUser.RoleMappings)
                .Query()
                .Include(mapping => mapping.Role)
                .LoadAsync(cancellationToken);
        }

        private async Task<Role> GetRoleEntityAsync(string role, CancellationToken cancellationToken)
        {
            var normalizedName = role.ToUpperInvariant();

            return await _dbContext.Roles.FirstAsync(
                existingRole => existingRole.NormalizedName == normalizedName,
                cancellationToken);
        }
    }
}
