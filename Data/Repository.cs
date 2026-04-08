using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data
{
    public sealed class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        private readonly DbSet<TEntity> _dbSet;

        public Repository(ExpenseTrackerDbContext dbContext)
        {
            _dbSet = dbContext.Set<TEntity>();
        }

        public IQueryable<TEntity> Query() => _dbSet;

        public ValueTask<TEntity?> FindAsync(params object[] keyValues) => _dbSet.FindAsync(keyValues);

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
            _dbSet.AddAsync(entity, cancellationToken).AsTask();

        public void Update(TEntity entity) => _dbSet.Update(entity);

        public void Remove(TEntity entity) => _dbSet.Remove(entity);

        public void RemoveRange(IEnumerable<TEntity> entities) => _dbSet.RemoveRange(entities);
    }
}
