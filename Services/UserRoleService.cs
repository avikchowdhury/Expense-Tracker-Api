using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;

namespace ExpenseTracker.Api.Services
{
    public sealed class UserRoleService : IUserRoleService
    {
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UserRoleService(
            IUserRoleRepository userRoleRepository,
            IUnitOfWork unitOfWork)
        {
            _userRoleRepository = userRoleRepository;
            _unitOfWork = unitOfWork;
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
                var exists = await _userRoleRepository.RoleExistsAsync(normalizedName, cancellationToken);

                if (!exists)
                {
                    await _userRoleRepository.AddRoleAsync(new Role
                    {
                        Name = roleName,
                        NormalizedName = normalizedName
                    }, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
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

            await _userRoleRepository.LoadRoleMappingsAsync(user, cancellationToken);

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

            await _userRoleRepository.LoadRoleMappingsAsync(user, cancellationToken);

            if (user.RoleMappings.Count > 0)
            {
                _userRoleRepository.RemoveRoleMappings(user.RoleMappings);
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

            return await _userRoleRepository.CountUsersInRoleAsync(
                normalizedRole,
                normalizedName,
                cancellationToken);
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _userRoleRepository.LoadRoleMappingsAsync(user, cancellationToken);
        }

        private async Task<Role> GetRoleEntityAsync(string role, CancellationToken cancellationToken)
        {
            var normalizedName = role.ToUpperInvariant();

            return await _userRoleRepository.GetRoleByNormalizedNameAsync(normalizedName, cancellationToken);
        }
    }
}
