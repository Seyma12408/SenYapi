using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Entity;

namespace SenYapi.Web.Controllers;

public class ProjectController : Controller
{
    private readonly IProjectService _projectService;
    private readonly IUrlRedirectService _redirectService;
    private readonly IFavoriteService _favoriteService;
    private readonly IProjectInquiryService _inquiryService;
    private readonly IProjectAppointmentService _appointmentService;
    private readonly UserManager<AppUser> _userManager;

    public ProjectController(
        IProjectService projectService,
        IUrlRedirectService redirectService,
        IFavoriteService favoriteService,
        IProjectInquiryService inquiryService,
        IProjectAppointmentService appointmentService,
        UserManager<AppUser> userManager)
    {
        _projectService = projectService;
        _redirectService = redirectService;
        _favoriteService = favoriteService;
        _inquiryService = inquiryService;
        _appointmentService = appointmentService;
        _userManager = userManager;
    }

    [HttpGet]
    [Route("Project")]
    [Route("projeler")]
    [Route("Project/Index")]
    public async Task<IActionResult> Index()
    {
        var projects = await _projectService.GetAllProjectsAsync(onlyActive: true);
        return View(projects);
    }

    [HttpGet]
    [Route("projeler/{slug}")]
    [Route("Project/Detail/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        // 301 Redirect Check
        var currentPath = $"/projeler/{slug.ToLowerInvariant()}";
        var redirectUrl = await _redirectService.CheckRedirectAsync(currentPath);
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            return RedirectPermanent(redirectUrl);
        }

        // Try find by slug first
        var project = await _projectService.GetProjectBySlugAsync(slug);

        // Fallback: if slug is integer id
        if (project == null && int.TryParse(slug, out int id))
        {
            project = await _projectService.GetProjectByIdAsync(id);
            if (project != null && !string.IsNullOrEmpty(project.Slug))
            {
                return RedirectPermanent($"/projeler/{project.Slug}");
            }
        }

        if (project == null || !project.IsActive)
            return NotFound();

        // Check if current user favorited this project
        bool isFavorite = false;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                isFavorite = await _favoriteService.IsFavoriteAsync(userId, project.Id);
                ViewBag.CurrentUser = await _userManager.GetUserAsync(User);
            }
        }
        ViewBag.IsFavorite = isFavorite;

        return View(project);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite([FromForm] int projectId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "Lütfen giriş yapınız." });
        }

        var isFavorite = await _favoriteService.ToggleFavoriteAsync(userId, projectId);
        return Json(new
        {
            success = true,
            isFavorite = isFavorite,
            message = isFavorite ? "Proje favorilerinize eklendi." : "Proje favorilerinizden çıkarıldı."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitInquiry(
        [FromForm] int projectId,
        [FromForm] string fullName,
        [FromForm] string email,
        [FromForm] string phone,
        [FromForm] string message,
        [FromForm] string preferredContactMethod)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
        {
            return Json(new { success = false, message = "Lütfen ad soyad, e-posta ve mesaj alanlarını doldurunuz." });
        }

        string? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            userId = _userManager.GetUserId(User);
        }

        var inquiry = new ProjectInquiry
        {
            ProjectId = projectId,
            UserId = userId,
            FullName = fullName.Trim(),
            Email = email.Trim(),
            Phone = phone?.Trim() ?? string.Empty,
            Message = message.Trim(),
            PreferredContactMethod = !string.IsNullOrWhiteSpace(preferredContactMethod) ? preferredContactMethod : "E-posta",
            Status = "Beklemede",
            CreatedAt = DateTime.UtcNow
        };

        await _inquiryService.CreateInquiryAsync(inquiry);

        return Json(new
        {
            success = true,
            message = "Bilgi talebiniz başarıyla iletildi! Uzman temsilcimiz en kısa sürede sizinle iletişime geçecektir."
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAppointment(
        [FromForm] int projectId,
        [FromForm] DateTime preferredDate,
        [FromForm] string preferredTimeSlot,
        [FromForm] string? phone,
        [FromForm] string? notes)
    {
        if (preferredDate.Date < DateTime.Today)
        {
            return Json(new { success = false, message = "Geçmiş bir tarih için randevu talep edilemez." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, message = "Kullanıcı oturumu bulunamadı." });
        }

        var appointment = new ProjectAppointment
        {
            ProjectId = projectId,
            UserId = user.Id,
            FullName = user.FullName ?? user.UserName ?? "Kullanıcı",
            Email = user.Email ?? "",
            Phone = !string.IsNullOrWhiteSpace(phone) ? phone.Trim() : (user.PhoneNumber ?? ""),
            PreferredDate = preferredDate.Date,
            PreferredTimeSlot = !string.IsNullOrWhiteSpace(preferredTimeSlot) ? preferredTimeSlot : "10:00 - 12:00",
            Notes = notes?.Trim(),
            Status = "Beklemede",
            CreatedAt = DateTime.UtcNow
        };

        var result = await _appointmentService.CreateAppointmentAsync(appointment);
        if (!result.Success)
        {
            return Json(new { success = false, message = result.Error ?? "Randevu talebi oluşturulamadı." });
        }

        return Json(new
        {
            success = true,
            message = "Randevu talebiniz başarıyla oluşturuldu. Talebinizin durumunu 'Hesabım > Randevularım' sayfasından takip edebilirsiniz."
        });
    }
}
