using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.Core.Entities;
using SenYapi.Core.Interfaces;

namespace SenYapi.DataAccess.Repositories;

public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly SenYapiDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(SenYapiDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.Where(e => e.IsActive).ToListAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(e => e.IsActive).Where(predicate).ToListAsync();
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void Delete(T entity)
    {
        _dbSet.Remove(entity);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate == null)
            return await _dbSet.CountAsync(e => e.IsActive);
        return await _dbSet.Where(e => e.IsActive).CountAsync(predicate);
    }
}
