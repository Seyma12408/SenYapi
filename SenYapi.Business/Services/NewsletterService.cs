using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SenYapi.Core.Interfaces;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class NewsletterService : INewsletterService
{
    private readonly IUnitOfWork _unitOfWork;

    public NewsletterService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> SubscribeAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var cleanEmail = email.Trim().ToLowerInvariant();

        var existing = (await _unitOfWork.Repository<NewsletterSubscriber>().GetAllAsync())
            .FirstOrDefault(s => s.Email.ToLower() == cleanEmail);

        if (existing != null) return true;

        var sub = new NewsletterSubscriber
        {
            Email = cleanEmail,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<NewsletterSubscriber>().AddAsync(sub);
        return await _unitOfWork.CommitAsync() > 0;
    }

    public async Task<IEnumerable<NewsletterSubscriber>> GetAllSubscribersAsync()
    {
        var subs = await _unitOfWork.Repository<NewsletterSubscriber>().GetAllAsync();
        return subs.OrderByDescending(s => s.CreatedAt);
    }

    public async Task DeleteSubscriberAsync(int id)
    {
        var sub = await _unitOfWork.Repository<NewsletterSubscriber>().GetByIdAsync(id);
        if (sub != null)
        {
            _unitOfWork.Repository<NewsletterSubscriber>().Delete(sub);
            await _unitOfWork.CommitAsync();
        }
    }
}
