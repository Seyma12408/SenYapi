using System;
using System.Collections;
using System.Threading.Tasks;
using SenYapi.Core.Entities;
using SenYapi.Core.Interfaces;

namespace SenYapi.DataAccess.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SenYapiDbContext _context;
    private readonly Hashtable _repositories = new();

    public UnitOfWork(SenYapiDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T).Name;

        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>);
            var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _context);
            _repositories.Add(type, repositoryInstance);
        }

        return (IRepository<T>)_repositories[type]!;
    }

    public async Task<int> CommitAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
