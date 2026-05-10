using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;

namespace ExpenseTracker.Api.Services
{
    public sealed class AdminUserDeletionService : IAdminUserDeletionService
    {
        private readonly IAdminUserDeletionRepository _adminUserDeletionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserRoleService _userRoleService;
        private readonly FileStoragePaths _storagePaths;
        private readonly ILogger<AdminUserDeletionService> _logger;

        public AdminUserDeletionService(
            IAdminUserDeletionRepository adminUserDeletionRepository,
            IUnitOfWork unitOfWork,
            IUserRoleService userRoleService,
            FileStoragePaths storagePaths,
            ILogger<AdminUserDeletionService> logger)
        {
            _adminUserDeletionRepository = adminUserDeletionRepository;
            _unitOfWork = unitOfWork;
            _userRoleService = userRoleService;
            _storagePaths = storagePaths;
            _logger = logger;
        }

        public async Task<AdminDeleteUsersResultDto> DeleteUsersAsync(
            int actingUserId,
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
        {
            var normalizedUserIds = (userIds ?? Array.Empty<int>())
                .Where(userId => userId > 0)
                .Distinct()
                .ToArray();

            if (normalizedUserIds.Length == 0)
            {
                throw new ArgumentException("Select at least one user to delete.");
            }

            if (normalizedUserIds.Contains(actingUserId))
            {
                throw new ArgumentException("Use another admin account to delete your own account.");
            }

            var users = await _adminUserDeletionRepository.GetUsersWithRolesAsync(
                normalizedUserIds,
                cancellationToken);

            if (users.Count == 0)
            {
                return new AdminDeleteUsersResultDto
                {
                    RequestedCount = normalizedUserIds.Length,
                    DeletedCount = 0,
                    Message = "No matching users were found."
                };
            }

            var deletedAdminCount = users.Count(user =>
                string.Equals(GetPrimaryRole(user), AppRoles.Admin, StringComparison.OrdinalIgnoreCase));
            if (deletedAdminCount > 0)
            {
                var adminCount = await _userRoleService.CountUsersInRoleAsync(AppRoles.Admin, cancellationToken);
                if (adminCount - deletedAdminCount < 1)
                {
                    throw new ArgumentException("At least one admin account must remain in the workspace.");
                }
            }

            var deletedUserIds = users
                .Select(user => user.Id)
                .ToArray();

            var deletionData = await _adminUserDeletionRepository.GetDeletionDataAsync(
                deletedUserIds,
                cancellationToken);

            var avatarPaths = users
                .Select(user => ResolveAvatarPath(user.AvatarUrl))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();
            var receiptPaths = deletionData.Receipts
                .Select(receipt => receipt.BlobUrl)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();

            _adminUserDeletionRepository.RemoveDeletionData(deletionData, users);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            DeleteFilesBestEffort(receiptPaths, _storagePaths.ReceiptsPath, "receipt");
            DeleteFilesBestEffort(avatarPaths, _storagePaths.AvatarsPath, "avatar");

            return new AdminDeleteUsersResultDto
            {
                RequestedCount = normalizedUserIds.Length,
                DeletedCount = users.Count,
                DeletedReceiptCount = deletionData.Receipts.Count,
                DeletedExpenseCount = deletionData.Expenses.Count,
                DeletedBudgetCount = deletionData.Budgets.Count,
                DeletedCategoryCount = deletionData.Categories.Count,
                DeletedVendorRuleCount = deletionData.VendorRules.Count,
                DeletedUserIds = deletedUserIds,
                Message = BuildSuccessMessage(
                    users.Count,
                    deletionData.Receipts.Count,
                    deletionData.Expenses.Count,
                    deletionData.Budgets.Count,
                    deletionData.Categories.Count,
                    deletionData.VendorRules.Count)
            };
        }

        private static string GetPrimaryRole(User user)
        {
            var mappedRole = user.RoleMappings
                .Select(mapping => mapping.Role?.Name)
                .FirstOrDefault(roleName => !string.IsNullOrWhiteSpace(roleName));

            return string.Equals(mappedRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(user.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? AppRoles.Admin
                : AppRoles.User;
        }

        private string? ResolveAvatarPath(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return null;
            }

            string? fileName = null;
            if (Uri.TryCreate(avatarUrl, UriKind.Absolute, out var absoluteUri))
            {
                fileName = Path.GetFileName(absoluteUri.LocalPath);
            }
            else
            {
                fileName = Path.GetFileName(avatarUrl);
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            return Path.Combine(_storagePaths.AvatarsPath, fileName);
        }

        private void DeleteFilesBestEffort(
            IEnumerable<string> candidatePaths,
            string allowedRoot,
            string label)
        {
            var normalizedRoot = Path.GetFullPath(allowedRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            foreach (var candidatePath in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var normalizedPath = Path.GetFullPath(candidatePath);
                    if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Skipping {Label} file cleanup outside allowed root: {Path}",
                            label,
                            normalizedPath);
                        continue;
                    }

                    if (File.Exists(normalizedPath))
                    {
                        File.Delete(normalizedPath);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to delete {Label} file {Path}",
                        label,
                        candidatePath);
                }
            }
        }

        private static string BuildSuccessMessage(
            int deletedUsers,
            int deletedReceipts,
            int deletedExpenses,
            int deletedBudgets,
            int deletedCategories,
            int deletedVendorRules)
        {
            return $"Deleted {deletedUsers} user{(deletedUsers == 1 ? string.Empty : "s")} and removed "
                + $"{deletedReceipts} receipt{(deletedReceipts == 1 ? string.Empty : "s")}, "
                + $"{deletedExpenses} expense{(deletedExpenses == 1 ? string.Empty : "s")}, "
                + $"{deletedBudgets} budget{(deletedBudgets == 1 ? string.Empty : "s")}, "
                + $"{deletedCategories} categor{(deletedCategories == 1 ? "y" : "ies")}, and "
                + $"{deletedVendorRules} vendor rule{(deletedVendorRules == 1 ? string.Empty : "s")}.";
        }
    }
}
