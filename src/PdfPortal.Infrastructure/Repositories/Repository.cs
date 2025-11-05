using Microsoft.EntityFrameworkCore;
using PdfPortal.Application.Interfaces;
using PdfPortal.Infrastructure.Data;
using System.Linq.Expressions;

namespace PdfPortal.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly PdfPortalDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(PdfPortalDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        int? skip = null,
        int? take = null,
        Expression<Func<T, object>>? orderBy = null,
        bool orderByDescending = false)
    {
        var query = _dbSet.Where(predicate);

        if (orderBy != null)
        {
            query = orderByDescending 
                ? query.OrderByDescending(orderBy) 
                : query.OrderBy(orderBy);
        }

        if (skip.HasValue)
        {
            query = query.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync();
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }
}
