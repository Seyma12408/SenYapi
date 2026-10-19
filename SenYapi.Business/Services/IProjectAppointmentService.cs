using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IProjectAppointmentService
{
    Task<(bool Success, string? Error)> CreateAppointmentAsync(ProjectAppointment appointment);
    Task<IEnumerable<ProjectAppointment>> GetUserAppointmentsAsync(string userId);
    Task<IEnumerable<ProjectAppointment>> GetAllAppointmentsAsync();
    Task<ProjectAppointment?> GetAppointmentByIdAsync(int id);
    Task UpdateAppointmentStatusAsync(int id, string status, string? adminNotes, DateTime? alternativeDate, string adminUserName);
    Task UserAcceptAlternativeDateAsync(int id, string userId);
    Task UserDeclineAlternativeDateAsync(int id, string userId);
    Task UserCancelAppointmentAsync(int id, string userId);
}
