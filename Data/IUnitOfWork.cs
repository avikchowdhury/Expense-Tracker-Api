using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Data
{
    public interface IUnitOfWork
    {
        IRepository<User> Users { get; }
        IRepository<Receipt> Receipts { get; }
        IRepository<Expense> Expenses { get; }
        IRepository<Budget> Budgets { get; }
        IRepository<Category> Categories { get; }
        IRepository<VendorCategoryRule> VendorCategoryRules { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
