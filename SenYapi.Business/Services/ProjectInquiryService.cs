using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ProjectInquiryService : IProjectInquiryService
{
    private readonly SenYapiDbContext _context;
    private readonly INotificationService _notificationService;

    public ProjectInquiryService(SenYapiDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<ProjectInquiry> CreateInquiryAsync(ProjectInquiry inquiry)
    {
        inquiry.CreatedAt = DateTime.UtcNow;
        inquiry.Status = "Yeni";
        inquiry.IsActive = true;

        await _context.ProjectInquiries.AddAsync(inquiry);
        await _context.SaveChangesAsync();
        return inquiry;
    }

    public async Task<IEnumerable<ProjectInquiry>> GetUserInquiriesAsync(string userId)
    {
        return await _context.ProjectInquiries
            .Include(i => i.Project)
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProjectInquiry>> GetAllInquiriesAsync()
    {
        return await _context.ProjectInquiries
            .Include(i => i.Project)
            .Include(i => i.User)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<ProjectInquiry?> GetInquiryByIdAsync(int id)
    {
        return await _context.ProjectInquiries
            .Include(i => i.Project)
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<bool> ReplyInquiryAsync(int id, string replyText, string newStatus, string adminUserName)
    {
        var inquiry = await _context.ProjectInquiries.Include(i => i.Project).FirstOrDefaultAsync(i => i.Id == id);
        if (inquiry == null) return false;

        inquiry.AdminReply = replyText;
        inquiry.AdminReplyAt = DateTime.UtcNow;
        inquiry.Status = string.IsNullOrWhiteSpace(newStatus) ? "Tamamlandi" : newStatus;

        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(inquiry.UserId))
        {
            await _notificationService.SendNotificationAsync(
                inquiry.UserId,
                "Bilgi Talebiniz Yanıtlandı",
                $"'{inquiry.Project?.Title}' projesi için oluşturduğunuz bilgi talebi yönetici tarafından yanıtlandı.",
                "/hesap/talepler"
            );
        }

        return true;
    }

    public async Task UpdateInquiryStatusAsync(int id, string status)
    {
        var inquiry = await _context.ProjectInquiries.FindAsync(id);
        if (inquiry != null)
        {
            inquiry.Status = status;
            await _context.SaveChangesAsync();
        }
    }
}
