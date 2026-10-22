using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.DTOs;
using SenYapi.Business.Services;
using SenYapi.Entity;

namespace SenYapi.Web.Controllers;

public class ContactController : Controller
{
    private readonly IContactService _contactService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ICaptchaService _captchaService;
    private readonly IRateLimitService _rateLimitService;

    public ContactController(
        IContactService contactService, 
        UserManager<AppUser> userManager,
        ICaptchaService captchaService,
        IRateLimitService rateLimitService)
    {
        _contactService = contactService;
        _userManager = userManager;
        _captchaService = captchaService;
        _rateLimitService = rateLimitService;
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var ip = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(ip)) return ip;
        }
        var cfIp = Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfIp)) return cfIp.Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    [HttpGet]
    [Route("Contact")]
    [Route("iletisim")]
    [Route("Contact/Index")]
    public IActionResult Index()
    {
        return View(new ContactMessageDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(
        ContactMessageDto dto, 
        [FromForm(Name = "cf-turnstile-response")] string? turnstileToken = null)
    {
        var clientIp = GetClientIp();

        // 1. Rate Limiting (IP başına 10 dakikada en fazla 5 mesaj)
        var (rlAllowed, rlError) = _rateLimitService.CheckRateLimit("contact_form", clientIp, 5, TimeSpan.FromMinutes(10));
        if (!rlAllowed)
        {
            ModelState.AddModelError("", rlError ?? "Çok fazla iletişim mesajı gönderildi. Lütfen daha sonra tekrar deneyiniz.");
            return View("Index", dto);
        }

        // 2. CAPTCHA Doğrulaması
        var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, "contact", clientIp);
        if (!captchaValid)
        {
            ModelState.AddModelError("", captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.");
            return View("Index", dto);
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            dto.UserId = _userManager.GetUserId(User);
        }

        if (!ValidationHelper.IsCleanText(dto.Name, 2, 100))
        {
            ModelState.AddModelError(nameof(dto.Name), "Lütfen adınızı ve soyadınızı geçerli bir biçimde giriniz (en az 2 karakter).");
        }

        if (!ValidationHelper.IsValidEmail(dto.Email))
        {
            ModelState.AddModelError(nameof(dto.Email), "Lütfen geçerli bir e-posta adresi giriniz (Örn: ad.soyad@ornek.com).");
        }

        if (!ValidationHelper.IsCleanText(dto.Subject, 2, 150))
        {
            ModelState.AddModelError(nameof(dto.Subject), "Lütfen en az 2 karakterden oluşan bir konu başlığı giriniz.");
        }

        if (!ValidationHelper.IsCleanText(dto.Message, 5, 2000))
        {
            ModelState.AddModelError(nameof(dto.Message), "Lütfen en az 5, en fazla 2000 karakterden oluşan bir mesaj yazınız.");
        }

        if (ModelState.IsValid)
        {
            var success = await _contactService.SaveMessageAsync(dto);
            if (success)
            {
                TempData["SuccessMessage"] = "Mesajınız başarıyla iletildi. En kısa sürede sizinle iletişime geçeceğiz.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", "Mesaj kaydedilirken bir hata oluştu. Lütfen tekrar deneyiniz.");
        }
        return View("Index", dto);
    }

    [HttpGet]
    [Route("iletisim/mesaj/{id}")]
    public async Task<IActionResult> MessageThread(int id, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return NotFound("Geçersiz veya yetkisiz erişim bağlantısı.");
        }

        var currentUserId = User.Identity?.IsAuthenticated == true
            ? _userManager.GetUserId(User)
            : null;

        var message = await _contactService.GetMessageByTokenAsync(id, token, currentUserId);
        if (message == null)
        {
            return NotFound("Mesaj bulunamadı veya erişim anahtarı geçersiz.");
        }

        ViewBag.Token = token;
        return View(message);
    }

    [HttpPost]
    [Route("iletisim/mesaj/yanit-yaz")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerReply(
        int messageId, 
        string token, 
        string replyText, 
        string? customerName = null,
        [FromForm(Name = "cf-turnstile-response")] string? turnstileToken = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return NotFound("Geçersiz erişim bağlantısı.");
        }

        var clientIp = GetClientIp();

        // 1. Rate Limiting (IP başına 10 dakikada en fazla 10 yanıt)
        var (rlAllowed, rlError) = _rateLimitService.CheckRateLimit("contact_reply", clientIp, 10, TimeSpan.FromMinutes(10));
        if (!rlAllowed)
        {
            TempData["ThreadError"] = rlError ?? "Çok fazla yanıt gönderildi. Lütfen biraz bekleyiniz.";
            TempData["PreservedReplyText"] = replyText;
            TempData["PreservedCustomerName"] = customerName;
            return Redirect($"/iletisim/mesaj/{messageId}?token={token}#yanit-alani");
        }

        // 2. CAPTCHA Doğrulaması
        var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, "contact_reply", clientIp);
        if (!captchaValid)
        {
            TempData["ThreadError"] = captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.";
            TempData["PreservedReplyText"] = replyText;
            TempData["PreservedCustomerName"] = customerName;
            return Redirect($"/iletisim/mesaj/{messageId}?token={token}#yanit-alani");
        }

        if (string.IsNullOrWhiteSpace(replyText) || replyText.Trim().Length < 3)
        {
            TempData["ThreadError"] = "Lütfen en az 3 karakterden oluşan bir yanıt yazınız.";
            TempData["PreservedReplyText"] = replyText;
            TempData["PreservedCustomerName"] = customerName;
            return Redirect($"/iletisim/mesaj/{messageId}?token={token}#yanit-alani");
        }

        var currentUserId = User.Identity?.IsAuthenticated == true
            ? _userManager.GetUserId(User)
            : null;

        var (success, error) = await _contactService.AddCustomerReplyAsync(messageId, token, replyText, currentUserId, customerName);
        if (success)
        {
            TempData["ThreadSuccess"] = "Yanıtınız iletildi. Yöneticimiz inceleyerek dönüş yapacaktır.";
        }
        else
        {
            TempData["ThreadError"] = error ?? "Yanıt iletilirken bir sorun oluştu.";
            TempData["PreservedReplyText"] = replyText;
            TempData["PreservedCustomerName"] = customerName;
        }

        return Redirect($"/iletisim/mesaj/{messageId}?token={token}#yanit-alani");
    }
}
