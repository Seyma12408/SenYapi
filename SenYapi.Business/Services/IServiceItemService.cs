using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IServiceItemService
{
    Task<IEnumerable<ServiceItem>> GetAllServicesAsync();
    Task<ServiceItem?> GetServiceByIdAsync(int id);
    Task AddServiceAsync(ServiceItem service);
    Task UpdateServiceAsync(ServiceItem service);
    Task DeleteServiceAsync(int id);
}
