using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class SiteSettingService : ISiteSettingService
{
    private readonly SenYapiDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public SiteSettingService(SenYapiDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<SiteSetting> GetSettingsAsync()
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSetting();
            await _context.SiteSettings.AddAsync(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    public async Task UpdateSettingsAsync(SiteSetting setting, string adminUserName)
    {
        var current = await GetSettingsAsync();

        current.SiteTitle = setting.SiteTitle;
        current.Tagline = setting.Tagline;
        current.Phone = setting.Phone;
        current.Email = setting.Email;
        current.Address = setting.Address;
        current.WorkingHours = setting.WorkingHours;
        current.TwitterUrl = setting.TwitterUrl;
        current.FacebookUrl = setting.FacebookUrl;
        current.InstagramUrl = setting.InstagramUrl;
        current.LinkedInUrl = setting.LinkedInUrl;
        current.YouTubeUrl = setting.YouTubeUrl;
        current.HeroTitle = setting.HeroTitle;
        current.HeroSubtitle = setting.HeroSubtitle;

        if (!string.IsNullOrEmpty(setting.LogoUrl))
            current.LogoUrl = setting.LogoUrl;

        if (!string.IsNullOrEmpty(setting.FaviconUrl))
            current.FaviconUrl = setting.FaviconUrl;

        current.MaxFailedAccessAttempts = setting.MaxFailedAccessAttempts;
        current.DefaultLockoutMinutes = setting.DefaultLockoutMinutes;

        _context.SiteSettings.Update(current);
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(adminUserName, "Güncelleme", "SiteSetting", "Site ve güvenlik ayarları güncellendi.");
    }
}
