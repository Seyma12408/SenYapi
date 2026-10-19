using Microsoft.Extensions.DependencyInjection;
using SenYapi.Core.Interfaces;
using SenYapi.DataAccess.Repositories;
using SenYapi.Business.Services;

namespace SenYapi.Business.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<INewsletterService, NewsletterService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IServiceItemService, ServiceItemService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITestimonialService, TestimonialService>();
        services.AddScoped<IBlogService, BlogService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ISiteSettingService, SiteSettingService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUrlRedirectService, UrlRedirectService>();

        // Portal, Favorites, Inquiries, Appointments, Messaging, Notifications, Versions
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IProjectInquiryService, ProjectInquiryService>();
        services.AddScoped<IProjectAppointmentService, ProjectAppointmentService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IContentVersionService, ContentVersionService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IPermissionService, PermissionService>();

        // SMS & Phone Password Reset
        services.AddHttpClient();
        services.AddHttpClient("TurnstileClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddMemoryCache();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IPhoneResetService, PhoneResetService>();

        // CAPTCHA, Rate Limiting & Login Protection
        services.AddScoped<ICaptchaService, TurnstileService>();
        services.AddScoped<IRateLimitService, RateLimitService>();
        services.AddScoped<ILoginProtectionService, LoginProtectionService>();

        return services;
    }
}
