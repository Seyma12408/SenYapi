using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ProjectAppointmentService : IProjectAppointmentService
{
    private readonly SenYapiDbContext _context;
    private readonly INotificationService _notificationService;

    public ProjectAppointmentService(SenYapiDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<(bool Success, string? Error)> CreateAppointmentAsync(ProjectAppointment appointment)
    {
        if (appointment.AppointmentDate <= DateTime.UtcNow)
        {
            return (false, "Geçmiş bir tarih ve saat için randevu oluşturulamaz.");
        }

        appointment.CreatedAt = DateTime.UtcNow;
        appointment.Status = "Beklemede";
        appointment.IsActive = true;

        await _context.ProjectAppointments.AddAsync(appointment);
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<IEnumerable<ProjectAppointment>> GetUserAppointmentsAsync(string userId)
    {
        return await _context.ProjectAppointments
            .Include(a => a.Project)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProjectAppointment>> GetAllAppointmentsAsync()
    {
        return await _context.ProjectAppointments
            .Include(a => a.Project)
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<ProjectAppointment?> GetAppointmentByIdAsync(int id)
    {
        return await _context.ProjectAppointments
            .Include(a => a.Project)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task UpdateAppointmentStatusAsync(int id, string status, string? adminNotes, DateTime? alternativeDate, string adminUserName)
    {
        var app = await _context.ProjectAppointments.Include(a => a.Project).FirstOrDefaultAsync(a => a.Id == id);
        if (app != null)
        {
            app.Status = status;
            app.AdminNotes = adminNotes;
            if (alternativeDate.HasValue)
            {
                app.SuggestedAlternativeDate = alternativeDate.Value;
            }

            await _context.SaveChangesAsync();

            var message = status switch
            {
                "Onaylandi" => $"'{app.Project?.Title}' projesi için {app.AppointmentDate:dd.MM.yyyy HH:mm} tarihindeki randevunuz onaylandı.",
                "AlternatifTarihOnerildi" => $"'{app.Project?.Title}' projesi için {app.SuggestedAlternativeDate:dd.MM.yyyy HH:mm} alternatif tarihi önerildi.",
                "Reddedildi" => $"'{app.Project?.Title}' projesi için randevu talebiniz maalesef uygun bulunmadı.",
                _ => $"'{app.Project?.Title}' projesi için randevu durumunuz güncellendi: {status}"
            };

            await _notificationService.SendNotificationAsync(app.UserId, "Randevu Durumu Güncellendi", message, "/hesap/randevular");
        }
    }

    public async Task UserAcceptAlternativeDateAsync(int id, string userId)
    {
        var app = await _context.ProjectAppointments.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (app != null && app.SuggestedAlternativeDate.HasValue)
        {
            app.AppointmentDate = app.SuggestedAlternativeDate.Value;
            app.SuggestedAlternativeDate = null;
            app.Status = "Onaylandi";
            await _context.SaveChangesAsync();
        }
    }

    public async Task UserDeclineAlternativeDateAsync(int id, string userId)
    {
        var app = await _context.ProjectAppointments.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (app != null)
        {
            app.Status = "IptalEdildi";
            await _context.SaveChangesAsync();
        }
    }

    public async Task UserCancelAppointmentAsync(int id, string userId)
    {
        var app = await _context.ProjectAppointments.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (app != null)
        {
            app.Status = "IptalEdildi";
            await _context.SaveChangesAsync();
        }
    }
}
