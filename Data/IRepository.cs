using System.Linq.Expressions;

namespace ExpenseTracker.Api.Data
{
    public interface IRepository<TEntity> where TEntity : class
    {
        IQueryable<TEntity> Query();
        ValueTask<TEntity?> FindAsync(params object[] keyValues);
        Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        void Update(TEntity entity);
        void Remove(TEntity entity);
        void RemoveRange(IEnumerable<TEntity> entities);
    }
}
