using System;
using System.Threading.Tasks;
using SenYapi.Core.Entities;

namespace SenYapi.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> CommitAsync();
}
