using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Data
{
    public interface IAdminUserDeletionRepository
    {
        Task<List<User>> GetUsersWithRolesAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);
        Task<AdminUserDeletionData> GetDeletionDataAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);
        void RemoveDeletionData(AdminUserDeletionData deletionData, IEnumerable<User> users);
    }

    public sealed class AdminUserDeletionData
    {
        public List<Receipt> Receipts { get; init; } = new();
        public List<Expense> Expenses { get; init; } = new();
        public List<Budget> Budgets { get; init; } = new();
        public List<VendorCategoryRule> VendorRules { get; init; } = new();
        public List<Category> Categories { get; init; } = new();
        public List<UserRoleMapping> RoleMappings { get; init; } = new();
    }
}
