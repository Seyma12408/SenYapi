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
    [ValidateAntiForgeryToken]
    [Route("Project/ToggleFavorite")]
    [Route("proje/favori-ekle-cikar")]
    public async Task<IActionResult> ToggleFavorite([FromForm] int projectId, [FromForm] string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            var targetReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : "/projeler";

            return Json(new
            {
                success = false,
                requireLogin = true,
                loginUrl = $"/giris-yap?returnUrl={Uri.EscapeDataString(targetReturnUrl)}",
                message = "Favorilere eklemek için lütfen giriş yapınız."
            });
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new
            {
                success = false,
                requireLogin = true,
                loginUrl = "/giris-yap",
                message = "Lütfen giriş yapınız."
            });
        }

        if (projectId <= 0)
        {
            return Json(new { success = false, message = "Geçersiz proje bilgisi." });
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
        [FromForm] string? phone,
        [FromForm] string message,
        [FromForm] string preferredContactMethod)
    {
        // 1. Proje Kontrolü
        var project = await _projectService.GetProjectByIdAsync(projectId);
        if (project == null || project.IsDeleted)
        {
            return Json(new { success = false, message = "Talep oluşturulmak istenen proje bulunamadı veya yayından kaldırılmış." });
        }

        // 2. Ad Soyad Kontrolü
        if (!ValidationHelper.IsCleanText(fullName, minLength: 2, maxLength: 100))
        {
            return Json(new { success = false, message = "Lütfen geçerli bir ad ve soyad giriniz (en az 2 karakter)." });
        }

        // 3. E-Posta Kontrolü
        if (!ValidationHelper.IsValidEmail(email))
        {
            return Json(new { success = false, message = "Lütfen geçerli bir e-posta adresi giriniz. (Örn: ad.soyad@example.com)" });
        }

        // 4. Tercih Edilen İletişim Kanalı Kontrolü
        var contactMethod = !string.IsNullOrWhiteSpace(preferredContactMethod) ? preferredContactMethod.Trim() : "E-posta";
        if (!ValidationHelper.IsValidContactMethod(contactMethod))
        {
            contactMethod = "E-posta";
        }

        // 5. Telefon Numarası Kontrolü ve Normalizasyonu
        string savedPhone = string.Empty;
        if (contactMethod.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(phone) || !PhoneHelper.IsValidMobile(phone))
            {
                return Json(new { success = false, message = "WhatsApp ile iletişim seçildiğinde geçerli bir Türkiye cep telefonu numarası zorunludur. (Örn: 0532 123 45 67)" });
            }
            savedPhone = PhoneHelper.FormatDisplay(phone);
        }
        else if (contactMethod.Equals("Telefon", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(phone) || !PhoneHelper.IsValid(phone, allowLandline: true))
            {
                return Json(new { success = false, message = "Telefon görüşmesi için geçerli bir telefon numarası giriniz. (Örn: 0532 123 45 67 veya 0212 555 01 23)" });
            }
            savedPhone = PhoneHelper.FormatDisplay(phone);
        }
        else
        {
            // E-posta tercihinde telefon isteğe bağlıdır; ancak girilmişse geçerli olmalıdır
            if (!string.IsNullOrWhiteSpace(phone))
            {
                if (!PhoneHelper.IsValid(phone, allowLandline: true))
                {
                    return Json(new { success = false, message = "Girdiğiniz telefon numarası geçersizdir. Lütfen başında 0 bulunan ve 11 haneli (örn: 0532 123 45 67) veya +90 ile başlayan geçerli bir numara giriniz." });
                }
                savedPhone = PhoneHelper.FormatDisplay(phone);
            }
        }

        // 6. Mesaj Kontrolü
        if (!ValidationHelper.IsCleanText(message, minLength: 5, maxLength: 2000))
        {
            return Json(new { success = false, message = "Lütfen en az 5, en fazla 2000 karakterden oluşan bir mesaj giriniz." });
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
            Email = email.Trim().ToLowerInvariant(),
            Phone = savedPhone,
            Message = message.Trim(),
            PreferredContactMethod = contactMethod,
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
        // 1. Proje Kontrolü
        var project = await _projectService.GetProjectByIdAsync(projectId);
        if (project == null || project.IsDeleted)
        {
            return Json(new { success = false, message = "Randevu talep edilen proje bulunamadı veya yayından kaldırılmış." });
        }

        // 2. Tarih Kontrolü
        if (!ValidationHelper.IsValidAppointmentDate(preferredDate))
        {
            return Json(new { success = false, message = "Lütfen bugünden itibaren geçerli bir randevu tarihi seçiniz (en fazla 1 yıl sonrası)." });
        }

        // 3. Saat Aralığı Kontrolü
        if (!ValidationHelper.IsValidTimeSlot(preferredTimeSlot))
        {
            return Json(new { success = false, message = "Lütfen listeden geçerli bir randevu saat aralığı seçiniz." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, message = "Kullanıcı oturumu bulunamadı. Lütfen tekrar giriş yapınız." });
        }

        // 4. Telefon Numarası Kontrolü (Randevu onayı için zorunlu)
        var targetPhone = !string.IsNullOrWhiteSpace(phone) ? phone.Trim() : (user.PhoneNumber ?? string.Empty);
        if (string.IsNullOrWhiteSpace(targetPhone) || !PhoneHelper.IsValid(targetPhone, allowLandline: true))
        {
            return Json(new { success = false, message = "Randevu onayı için geçerli bir iletişim numarası girilmesi zorunludur. (Örn: 0532 123 45 67 veya 0212 555 01 23)" });
        }

        // 5. Notlar Uzunluk Kontrolü
        if (notes != null && notes.Length > 1000)
        {
            return Json(new { success = false, message = "Ek notlar en fazla 1000 karakter olabilir." });
        }

        var appointment = new ProjectAppointment
        {
            ProjectId = projectId,
            UserId = user.Id,
            FullName = user.FullName ?? user.UserName ?? "Kullanıcı",
            Email = user.Email ?? "",
            Phone = PhoneHelper.FormatDisplay(targetPhone),
            PreferredDate = preferredDate.Date,
            PreferredTimeSlot = preferredTimeSlot.Trim(),
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
