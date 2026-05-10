using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data
{
    public sealed class AdminUserDeletionRepository : IAdminUserDeletionRepository
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public AdminUserDeletionRepository(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<List<User>> GetUsersWithRolesAsync(
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .Include(user => user.RoleMappings)
                .ThenInclude(mapping => mapping.Role)
                .Where(user => userIds.Contains(user.Id))
                .ToListAsync(cancellationToken);
        }

        public async Task<AdminUserDeletionData> GetDeletionDataAsync(
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
        {
            return new AdminUserDeletionData
            {
                Receipts = await _dbContext.Receipts
                    .Where(receipt => userIds.Contains(receipt.UserId))
                    .ToListAsync(cancellationToken),
                Expenses = await _dbContext.Expenses
                    .Where(expense => userIds.Contains(expense.UserId))
                    .ToListAsync(cancellationToken),
                Budgets = await _dbContext.Budgets
                    .Where(budget => userIds.Contains(budget.UserId))
                    .ToListAsync(cancellationToken),
                VendorRules = await _dbContext.VendorCategoryRules
                    .Where(rule => userIds.Contains(rule.UserId))
                    .ToListAsync(cancellationToken),
                Categories = await _dbContext.Categories
                    .Where(category => userIds.Contains(category.UserId))
                    .ToListAsync(cancellationToken),
                RoleMappings = await _dbContext.UserRoleMappings
                    .Where(mapping => userIds.Contains(mapping.UserId))
                    .ToListAsync(cancellationToken)
            };
        }

        public void RemoveDeletionData(AdminUserDeletionData deletionData, IEnumerable<User> users)
        {
            if (deletionData.Expenses.Count > 0)
            {
                _dbContext.Expenses.RemoveRange(deletionData.Expenses);
            }

            if (deletionData.VendorRules.Count > 0)
            {
                _dbContext.VendorCategoryRules.RemoveRange(deletionData.VendorRules);
            }

            if (deletionData.Receipts.Count > 0)
            {
                _dbContext.Receipts.RemoveRange(deletionData.Receipts);
            }

            if (deletionData.Budgets.Count > 0)
            {
                _dbContext.Budgets.RemoveRange(deletionData.Budgets);
            }

            if (deletionData.RoleMappings.Count > 0)
            {
                _dbContext.UserRoleMappings.RemoveRange(deletionData.RoleMappings);
            }

            if (deletionData.Categories.Count > 0)
            {
                _dbContext.Categories.RemoveRange(deletionData.Categories);
            }

            _dbContext.Users.RemoveRange(users);
        }
    }
}
