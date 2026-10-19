using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
public class AdminAppointmentsController : AdminBaseController
{
    private readonly IProjectAppointmentService _appointmentService;
    private readonly IAuditLogService _auditLogService;

    public AdminAppointmentsController(
        IProjectAppointmentService appointmentService,
        IAuditLogService auditLogService)
    {
        _appointmentService = appointmentService;
        _auditLogService = auditLogService;
    }

    [HttpGet("Appointments")]
    public async Task<IActionResult> Appointments(string? status)
    {
        var appointments = await _appointmentService.GetAllAppointmentsAsync();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            appointments = appointments.Where(a => a.Status == status);
        }

        ViewBag.Status = status;
        return View("~/Views/Admin/Appointments.cshtml", appointments.ToList());
    }

    [HttpPost("UpdateAppointmentStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, string status, string? adminNotes, DateTime? alternativeDate)
    {
        await _appointmentService.UpdateAppointmentStatusAsync(id, status, adminNotes, alternativeDate, CurrentAdminName);
        TempData["AdminSuccess"] = "Randevu durumu başarıyla güncellendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Randevu Durum Güncelleme", "ProjectAppointment", $"AppointmentId: {id}, Durum: {status}");
        return RedirectToAction(nameof(Appointments));
    }
}
