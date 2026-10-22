using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Entity;

namespace SenYapi.Web.Controllers;

[Authorize]
public class PortalController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IFavoriteService _favoriteService;
    private readonly IProjectInquiryService _inquiryService;
    private readonly IProjectAppointmentService _appointmentService;
    private readonly IConversationService _conversationService;
    private readonly INotificationService _notificationService;
    private readonly IBlogService _blogService;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;

    public PortalController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IFavoriteService favoriteService,
        IProjectInquiryService inquiryService,
        IProjectAppointmentService appointmentService,
        IConversationService conversationService,
        INotificationService notificationService,
        IBlogService blogService,
        IFileService fileService,
        IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _favoriteService = favoriteService;
        _inquiryService = inquiryService;
        _appointmentService = appointmentService;
        _conversationService = conversationService;
        _notificationService = notificationService;
        _blogService = blogService;
        _fileService = fileService;
        _auditLogService = auditLogService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // 1. Dashboard entry
    [HttpGet]
    [Route("hesap")]
    [Route("profilim")]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Profile));
    }

    // 2. Profile & Settings
    [HttpGet]
    [Route("hesap/profil")]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(user.Id);
        return View(user);
    }

    [HttpPost]
    [Route("hesap/profil-guncelle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string? phoneNumber, IFormFile? profilePhoto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // 1. Ad Soyad Doğrulaması
        if (!ValidationHelper.IsCleanText(fullName, minLength: 2, maxLength: 100))
        {
            TempData["PortalError"] = "Ad ve soyad alanı zorunludur ve en az 2 karakter olmalıdır.";
            return RedirectToAction(nameof(Profile));
        }

        // 2. Telefon Numarası Doğrulaması (İsteğe bağlı, ancak girilmişse geçerli Türkiye cep telefonu olmalı)
        string? cleanPhone = null;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (!PhoneHelper.IsValidMobile(phoneNumber))
            {
                TempData["PortalError"] = "Geçersiz telefon numarası. Lütfen başında 0 bulunan 11 haneli veya +90 ile başlayan geçerli bir cep telefonu numarası giriniz. (Örn: 0532 123 45 67)";
                return RedirectToAction(nameof(Profile));
            }
            cleanPhone = PhoneHelper.FormatDisplay(phoneNumber);
        }

        user.FullName = fullName.Trim();
        user.PhoneNumber = cleanPhone;

        if (profilePhoto != null && profilePhoto.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(profilePhoto, "avatars");
            if (success)
            {
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    _fileService.DeleteFile(user.ProfilePictureUrl);
                }
                user.ProfilePictureUrl = filePath;
            }
            else
            {
                TempData["PortalError"] = error;
                return RedirectToAction(nameof(Profile));
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["PortalSuccess"] = "Profil bilgileriniz başarıyla güncellendi.";
            await _auditLogService.LogAsync(user.UserName!, "Profil Güncelleme", "AppUser", "Profil güncellendi.");
        }
        else
        {
            var errorList = string.Join(", ", result.Errors.Select(e => e.Description));
            TempData["PortalError"] = $"Profil güncellenirken hata oluştu: {errorList}";
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Route("hesap/profil-fotografi-kaldir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProfilePicture()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            _fileService.DeleteFile(user.ProfilePictureUrl);
            user.ProfilePictureUrl = null;
            await _userManager.UpdateAsync(user);
            TempData["PortalSuccess"] = "Profil fotoğrafınız kaldırıldı.";
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Route("hesap/sifre-degistir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            TempData["PortalError"] = "Lütfen mevcut şifrenizi giriniz.";
            return RedirectToAction(nameof(Profile));
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6 || newPassword.Length > 100)
        {
            TempData["PortalError"] = "Yeni şifre en az 6, en fazla 100 karakter olmalıdır.";
            return RedirectToAction(nameof(Profile));
        }

        if (newPassword != confirmNewPassword)
        {
            TempData["PortalError"] = "Yeni şifre ve şifre tekrarı uyuşmuyor.";
            return RedirectToAction(nameof(Profile));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["PortalSuccess"] = "Şifreniz başarıyla değiştirildi.";
            await _auditLogService.LogAsync(user.UserName!, "Şifre Değiştirme", "AppUser", "Kullanıcı şifresini değiştirdi.");
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            TempData["PortalError"] = $"Şifre değiştirilemedi: {errors}";
        }

        return RedirectToAction(nameof(Profile));
    }

    // 3. Favorites
    [HttpGet]
    [Route("hesap/favoriler")]
    public async Task<IActionResult> Favorites()
    {
        var favorites = await _favoriteService.GetUserFavoritesAsync(CurrentUserId);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(favorites);
    }

    [HttpPost]
    [Route("hesap/favori-sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFavorite(int projectId)
    {
        await _favoriteService.RemoveFavoriteAsync(CurrentUserId, projectId);
        TempData["PortalSuccess"] = "Proje favorilerinizden kaldırıldı.";
        return RedirectToAction(nameof(Favorites));
    }

    // 4. Inquiries
    [HttpGet]
    [Route("hesap/talepler")]
    public async Task<IActionResult> Inquiries()
    {
        var inquiries = await _inquiryService.GetUserInquiriesAsync(CurrentUserId);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(inquiries);
    }

    // 5. Appointments
    [HttpGet]
    [Route("hesap/randevular")]
    public async Task<IActionResult> Appointments()
    {
        var appointments = await _appointmentService.GetUserAppointmentsAsync(CurrentUserId);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(appointments);
    }

    [HttpPost]
    [Route("hesap/randevu-kabul")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptAlternativeDate(int id)
    {
        await _appointmentService.UserAcceptAlternativeDateAsync(id, CurrentUserId);
        TempData["PortalSuccess"] = "Önerilen alternatif randevu tarihini kabul ettiniz. Randevunuz kesinleşti.";
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost]
    [Route("hesap/randevu-ret")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineAlternativeDate(int id)
    {
        await _appointmentService.UserDeclineAlternativeDateAsync(id, CurrentUserId);
        TempData["PortalSuccess"] = "Önerilen tarihi uygun bulmadığınız için randevu iptal edildi.";
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost]
    [Route("hesap/randevu-iptal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(int id)
    {
        await _appointmentService.UserCancelAppointmentAsync(id, CurrentUserId);
        TempData["PortalSuccess"] = "Randevu talebiniz iptal edildi.";
        return RedirectToAction(nameof(Appointments));
    }

    // 6. Direct Messaging (Conversations)
    [HttpGet]
    [Route("hesap/mesajlar")]
    public async Task<IActionResult> Messages()
    {
        var conversations = await _conversationService.GetUserConversationsAsync(CurrentUserId);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(conversations);
    }

    [HttpGet]
    [Route("hesap/mesajlar/{id:int}")]
    public async Task<IActionResult> ConversationDetail(int id)
    {
        var conversation = await _conversationService.GetConversationByIdAsync(id);
        if (conversation == null || conversation.UserId != CurrentUserId)
        {
            return NotFound();
        }

        // Mark user read
        await _conversationService.MarkMessagesAsReadAsync(id, isAdmin: false);

        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(conversation);
    }

    [HttpPost]
    [Route("hesap/mesajlar/yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateConversation(string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["PortalError"] = "Konu ve mesaj alanları boş bırakılamaz.";
            return RedirectToAction(nameof(Messages));
        }

        var user = await _userManager.GetUserAsync(User);
        var senderName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : user?.UserName ?? "Kullanıcı";

        var conv = await _conversationService.CreateConversationAsync(CurrentUserId, subject, message, senderName);
        TempData["PortalSuccess"] = "Yeni mesaj konuşmanız başlatıldı. Yönetici yanıtı burada görünecektir.";

        return RedirectToAction(nameof(ConversationDetail), new { id = conv.Id });
    }

    [HttpPost]
    [Route("hesap/mesajlar/yanit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendConversationReply(int conversationId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["PortalError"] = "Mesaj boş olamaz.";
            return RedirectToAction(nameof(ConversationDetail), new { id = conversationId });
        }

        var conv = await _conversationService.GetConversationByIdAsync(conversationId);
        if (conv == null || conv.UserId != CurrentUserId)
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        var senderName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : user?.UserName ?? "Kullanıcı";

        await _conversationService.SendMessageAsync(conversationId, CurrentUserId, senderName, isAdmin: false, message);
        TempData["PortalSuccess"] = "Mesajınız iletildi.";

        return RedirectToAction(nameof(ConversationDetail), new { id = conversationId });
    }

    // 7. In-App Notifications
    [HttpGet]
    [Route("hesap/bildirimler")]
    public async Task<IActionResult> Notifications()
    {
        var notifications = await _notificationService.GetUserNotificationsAsync(CurrentUserId);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(notifications);
    }

    [HttpPost]
    [Route("hesap/bildirim-okundu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(int notificationId, string? returnUrl = null)
    {
        await _notificationService.MarkAsReadAsync(notificationId, CurrentUserId);
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost]
    [Route("hesap/bildirimler-tumunu-oku")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        TempData["PortalSuccess"] = "Tüm bildirimleriniz okundu olarak işaretlendi.";
        return RedirectToAction(nameof(Notifications));
    }

    // 8. User Blog Comments
    [HttpGet]
    [Route("hesap/yorumlar")]
    public async Task<IActionResult> Comments()
    {
        var comments = await _blogService.GetCommentsByUserIdAsync(CurrentUserId);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(comments);
    }

    // 9. Account Deactivation
    [HttpGet]
    [Route("hesap/kapat")]
    public async Task<IActionResult> DeactivateAccount()
    {
        var user = await _userManager.GetUserAsync(User);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(user);
    }

    [HttpPost]
    [Route("hesap/kapat-onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDeactivateAccount(string confirmPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
        {
            TempData["PortalError"] = "Sistem yöneticisi hesabı kapatılamaz veya silinemez.";
            return RedirectToAction(nameof(DeactivateAccount));
        }

        // Protect last admin
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count(u => !u.IsBlocked && !u.IsDeactivated);
            if (adminCount <= 1)
            {
                TempData["PortalError"] = "Sistemdeki son aktif yönetici hesabı dondurulamaz.";
                return RedirectToAction(nameof(DeactivateAccount));
            }
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, confirmPassword);
        if (!isPasswordValid)
        {
            TempData["PortalError"] = "Şifrenizi hatalı girdiniz. Hesap dondurulamadı.";
            return RedirectToAction(nameof(DeactivateAccount));
        }

        user.IsDeactivated = true;
        user.DeactivatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _signInManager.SignOutAsync();
        TempData["LoginInfo"] = "Hesabınız geçici olarak donduruldu. Tekrar giriş yaptığınızda hesabınızı dilediğiniz zaman yeniden etkinleştirebilirsiniz.";
        return RedirectToAction("Login", "Account");
    }

    // 10. Account Permanent Deletion Request
    [HttpGet]
    [Route("hesap/sil")]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await _userManager.GetUserAsync(User);
        ViewBag.UnreadNotifications = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return View(user);
    }

    [HttpPost]
    [Route("hesap/sil-onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDeleteAccount(string confirmPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
        {
            TempData["PortalError"] = "Sistem yöneticisi hesabı kapatılamaz veya silinemez.";
            return RedirectToAction(nameof(DeleteAccount));
        }

        // Protect last admin
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count(u => !u.IsBlocked && !u.IsDeactivated);
            if (adminCount <= 1)
            {
                TempData["PortalError"] = "Sistemdeki son aktif yönetici hesabı silinemez.";
                return RedirectToAction(nameof(DeleteAccount));
            }
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, confirmPassword);
        if (!isPasswordValid)
        {
            TempData["PortalError"] = "Şifrenizi hatalı girdiniz. Hesap silme talebi onaylanmadı.";
            return RedirectToAction(nameof(DeleteAccount));
        }

        // Anonymize user comments (preserves comment content while removing personal user data)
        var userComments = await _blogService.GetCommentsByUserIdAsync(user.Id);
        foreach (var c in userComments)
        {
            c.Name = "[Silinmiş Kullanıcı]";
            c.Email = "anonim@senyapi.com";
            c.UserId = null;
            await _blogService.UpdateCommentAsync(c);
        }

        if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            _fileService.DeleteFile(user.ProfilePictureUrl);
        }

        user.DeletionRequested = true;
        user.DeletionRequestedAt = DateTime.UtcNow;
        user.IsBlocked = true;
        user.UserName = $"deleted_{Guid.NewGuid():N}";
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.Email = $"deleted_{Guid.NewGuid():N}@senyapi.local";
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.FullName = "Silinmiş Kullanıcı";
        user.PhoneNumber = null;

        await _userManager.UpdateAsync(user);
        await _signInManager.SignOutAsync();

        TempData["LoginInfo"] = "Hesabınız başarıyla silinmiş ve kişisel verileriniz anonimleştirilmiştir.";
        return RedirectToAction("Login", "Account");
    }
}
