using System.Linq.Expressions;

namespace PdfPortal.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        int? skip = null,
        int? take = null,
        Expression<Func<T, object>>? orderBy = null,
        bool orderByDescending = false);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}
