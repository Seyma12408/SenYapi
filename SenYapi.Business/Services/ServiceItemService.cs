using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Core.Interfaces;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ServiceItemService : IServiceItemService
{
    private readonly IUnitOfWork _unitOfWork;

    public ServiceItemService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ServiceItem>> GetAllServicesAsync()
    {
        return await _unitOfWork.Repository<ServiceItem>().GetAllAsync();
    }

    public async Task<ServiceItem?> GetServiceByIdAsync(int id)
    {
        return await _unitOfWork.Repository<ServiceItem>().GetByIdAsync(id);
    }

    public async Task AddServiceAsync(ServiceItem service)
    {
        await _unitOfWork.Repository<ServiceItem>().AddAsync(service);
        await _unitOfWork.CommitAsync();
    }

    public async Task UpdateServiceAsync(ServiceItem service)
    {
        _unitOfWork.Repository<ServiceItem>().Update(service);
        await _unitOfWork.CommitAsync();
    }

    public async Task DeleteServiceAsync(int id)
    {
        var item = await _unitOfWork.Repository<ServiceItem>().GetByIdAsync(id);
        if (item != null)
        {
            _unitOfWork.Repository<ServiceItem>().Delete(item);
            await _unitOfWork.CommitAsync();
        }
    }
}
