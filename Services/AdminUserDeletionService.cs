using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public sealed class AdminUserDeletionService : IAdminUserDeletionService
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        private readonly IUserRoleService _userRoleService;
        private readonly FileStoragePaths _storagePaths;
        private readonly ILogger<AdminUserDeletionService> _logger;

        public AdminUserDeletionService(
            ExpenseTrackerDbContext dbContext,
            IUserRoleService userRoleService,
            FileStoragePaths storagePaths,
            ILogger<AdminUserDeletionService> logger)
        {
            _dbContext = dbContext;
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

            var users = await _dbContext.Users
                .Include(user => user.RoleMappings)
                .ThenInclude(mapping => mapping.Role)
                .Where(user => normalizedUserIds.Contains(user.Id))
                .ToListAsync(cancellationToken);

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

            var receipts = await _dbContext.Receipts
                .Where(receipt => deletedUserIds.Contains(receipt.UserId))
                .ToListAsync(cancellationToken);
            var expenses = await _dbContext.Expenses
                .Where(expense => deletedUserIds.Contains(expense.UserId))
                .ToListAsync(cancellationToken);
            var budgets = await _dbContext.Budgets
                .Where(budget => deletedUserIds.Contains(budget.UserId))
                .ToListAsync(cancellationToken);
            var vendorRules = await _dbContext.VendorCategoryRules
                .Where(rule => deletedUserIds.Contains(rule.UserId))
                .ToListAsync(cancellationToken);
            var categories = await _dbContext.Categories
                .Where(category => deletedUserIds.Contains(category.UserId))
                .ToListAsync(cancellationToken);
            var roleMappings = await _dbContext.UserRoleMappings
                .Where(mapping => deletedUserIds.Contains(mapping.UserId))
                .ToListAsync(cancellationToken);

            var avatarPaths = users
                .Select(user => ResolveAvatarPath(user.AvatarUrl))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();
            var receiptPaths = receipts
                .Select(receipt => receipt.BlobUrl)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();

            if (expenses.Count > 0)
            {
                _dbContext.Expenses.RemoveRange(expenses);
            }

            if (vendorRules.Count > 0)
            {
                _dbContext.VendorCategoryRules.RemoveRange(vendorRules);
            }

            if (receipts.Count > 0)
            {
                _dbContext.Receipts.RemoveRange(receipts);
            }

            if (budgets.Count > 0)
            {
                _dbContext.Budgets.RemoveRange(budgets);
            }

            if (roleMappings.Count > 0)
            {
                _dbContext.UserRoleMappings.RemoveRange(roleMappings);
            }

            if (categories.Count > 0)
            {
                _dbContext.Categories.RemoveRange(categories);
            }

            _dbContext.Users.RemoveRange(users);
            await _dbContext.SaveChangesAsync(cancellationToken);

            DeleteFilesBestEffort(receiptPaths, _storagePaths.ReceiptsPath, "receipt");
            DeleteFilesBestEffort(avatarPaths, _storagePaths.AvatarsPath, "avatar");

            return new AdminDeleteUsersResultDto
            {
                RequestedCount = normalizedUserIds.Length,
                DeletedCount = users.Count,
                DeletedReceiptCount = receipts.Count,
                DeletedExpenseCount = expenses.Count,
                DeletedBudgetCount = budgets.Count,
                DeletedCategoryCount = categories.Count,
                DeletedVendorRuleCount = vendorRules.Count,
                DeletedUserIds = deletedUserIds,
                Message = BuildSuccessMessage(
                    users.Count,
                    receipts.Count,
                    expenses.Count,
                    budgets.Count,
                    categories.Count,
                    vendorRules.Count)
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
