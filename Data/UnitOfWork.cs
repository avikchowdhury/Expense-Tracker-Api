using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Data
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        private IRepository<User>? _users;
        private IRepository<Receipt>? _receipts;
        private IRepository<Expense>? _expenses;
        private IRepository<Budget>? _budgets;
        private IRepository<Category>? _categories;
        private IRepository<VendorCategoryRule>? _vendorCategoryRules;

        public UnitOfWork(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IRepository<User> Users => _users ??= new Repository<User>(_dbContext);
        public IRepository<Receipt> Receipts => _receipts ??= new Repository<Receipt>(_dbContext);
        public IRepository<Expense> Expenses => _expenses ??= new Repository<Expense>(_dbContext);
        public IRepository<Budget> Budgets => _budgets ??= new Repository<Budget>(_dbContext);
        public IRepository<Category> Categories => _categories ??= new Repository<Category>(_dbContext);
        public IRepository<VendorCategoryRule> VendorCategoryRules => _vendorCategoryRules ??= new Repository<VendorCategoryRule>(_dbContext);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _dbContext.SaveChangesAsync(cancellationToken);
    }
}
