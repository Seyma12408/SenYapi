using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface INewsletterService
{
    Task<bool> SubscribeAsync(string email);
    Task<IEnumerable<NewsletterSubscriber>> GetAllSubscribersAsync();
    Task DeleteSubscriberAsync(int id);
}
