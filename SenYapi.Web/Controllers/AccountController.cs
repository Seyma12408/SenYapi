using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Entity;
using SenYapi.Web.Models;

namespace SenYapi.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly ISiteSettingService _siteSettingService;
    private readonly IPhoneResetService _phoneResetService;
    private readonly ICaptchaService _captchaService;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILoginProtectionService _loginProtectionService;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IEmailService emailService,
        ISiteSettingService siteSettingService,
        IPhoneResetService phoneResetService,
        ICaptchaService captchaService,
        IRateLimitService rateLimitService,
        ILoginProtectionService loginProtectionService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _siteSettingService = siteSettingService;
        _phoneResetService = phoneResetService;
        _captchaService = captchaService;
        _rateLimitService = rateLimitService;
        _loginProtectionService = loginProtectionService;
    }

    private string GetClientSessionKey()
    {
        const string cookieName = "SenYapi_Client_Id";
        var clientKey = Request.Cookies[cookieName];
        if (string.IsNullOrWhiteSpace(clientKey))
        {
            clientKey = Guid.NewGuid().ToString("N");
            Response.Cookies.Append(cookieName, clientKey, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });
        }
        return clientKey;
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
    [Route("giris-yap")]
    [Route("Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [Route("giris-yap")]
    [Route("Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model, 
        string? returnUrl = null, 
        [FromForm(Name = "cf-turnstile-response")] string? turnstileToken = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        var clientIp = GetClientIp();
        var clientKey = GetClientSessionKey();

        // 1. Cloudflare Turnstile CAPTCHA Doğrulaması (İlk açılıştan itibaren her girişte zorunlu)
        var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, "login", clientIp);
        if (!captchaValid)
        {
            ModelState.AddModelError("", captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Find user by UserName or Email
        var input = model.UsernameOrEmail.Trim();
        var user = await _userManager.FindByNameAsync(input) ?? await _userManager.FindByEmailAsync(input);

        if (user == null)
        {
            ModelState.AddModelError("", "Kullanıcı adı/e-posta veya şifre hatalı.");
            return View(model);
        }

        // Check if user is permanently blocked by Admin
        if (user.IsBlocked)
        {
            ModelState.AddModelError("", "Hesabınız yönetici tarafından engellenmiştir. Lütfen destek ile iletişime geçiniz.");
            return View(model);
        }

        // Check temporary lockout
        if (await _userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var remainingTime = lockoutEnd.HasValue ? lockoutEnd.Value - DateTimeOffset.UtcNow : TimeSpan.Zero;
            var minutes = Math.Max(1, (int)Math.Ceiling(remainingTime.TotalMinutes));
            ModelState.AddModelError("", $"Çok fazla hatalı giriş denemesi yapıldı. Hesabınız geçici olarak kilitlendi. Kalan süre: {minutes} dakika.");
            return View(model);
        }

        // Attempt Password Sign In with lockout enabled (true)
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Giriş başarılı -> Başarısız oturum sayacını sıfırla
            _loginProtectionService.ResetFailedAttempts(clientKey, clientIp);

            // If user was deactivated, reactivate automatically
            if (user.IsDeactivated)
            {
                user.IsDeactivated = false;
                user.DeactivatedAt = null;
                await _userManager.UpdateAsync(user);
                TempData["SuccessMessage"] = "Hoş geldiniz! Dondurulmuş olan hesabınız başarıyla yeniden aktif hale getirildi.";
            }

            // Reset access failed count on successful sign-in
            await _userManager.ResetAccessFailedCountAsync(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // Giriş başarısız -> Oturum başarısız sayacını artır
        _loginProtectionService.RecordFailedAttempt(clientKey, clientIp);

        if (result.IsLockedOut)
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var remainingTime = lockoutEnd.HasValue ? lockoutEnd.Value - DateTimeOffset.UtcNow : TimeSpan.Zero;
            var minutes = Math.Max(1, (int)Math.Ceiling(remainingTime.TotalMinutes));
            ModelState.AddModelError("", $"Arka arkaya hatalı şifre denemesi nedeniyle hesabınız {minutes} dakika süreyle kilitlenmiştir.");
            return View(model);
        }

        var failedAttempts = await _userManager.GetAccessFailedCountAsync(user);
        var settings = await _siteSettingService.GetSettingsAsync();
        var remainingAttempts = Math.Max(0, settings.MaxFailedAccessAttempts - failedAttempts);

        if (remainingAttempts > 0)
        {
            ModelState.AddModelError("", $"Kullanıcı adı/e-posta veya şifre hatalı. (Kalan deneme hakkınız: {remainingAttempts})");
        }
        else
        {
            ModelState.AddModelError("", "Kullanıcı adı/e-posta veya şifre hatalı.");
        }

        return View(model);
    }

    [HttpGet]
    [Route("kayit-ol")]
    [Route("Account/Register")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [Route("kayit-ol")]
    [Route("Account/Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        RegisterViewModel model, 
        [FromForm(Name = "cf-turnstile-response")] string? turnstileToken = null)
    {
        var clientIp = GetClientIp();

        // 1. Rate Limiting (IP başına 15 dakikada en fazla 5 kayıt isteği)
        var (rlAllowed, rlError) = _rateLimitService.CheckRateLimit("register", clientIp, 5, TimeSpan.FromMinutes(15));
        if (!rlAllowed)
        {
            ModelState.AddModelError("", rlError ?? "Çok fazla kayıt isteği yapıldı. Lütfen daha sonra tekrar deneyiniz.");
            return View(model);
        }

        // 2. CAPTCHA Doğrulaması
        var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, "register", clientIp);
        if (!captchaValid)
        {
            ModelState.AddModelError("", captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.");
            return View(model);
        }

        if (!ModelState.IsValid)
            return View(model);

        var cleanUserName = model.UserName.Trim();
        var cleanEmail = model.Email.Trim().ToLowerInvariant();

        // Unique username check
        var existingByUsername = await _userManager.FindByNameAsync(cleanUserName);
        if (existingByUsername != null)
        {
            ModelState.AddModelError("UserName", "Bu kullanıcı adı zaten kullanılmaktadır. Lütfen farklı bir kullanıcı adı seçiniz.");
            return View(model);
        }

        // Unique email check
        var existingByEmail = await _userManager.FindByEmailAsync(cleanEmail);
        if (existingByEmail != null)
        {
            ModelState.AddModelError("Email", "Bu e-posta adresi ile zaten kayıt olunmuş.");
            return View(model);
        }

        var user = new AppUser
        {
            UserName = cleanUserName,
            Email = cleanEmail,
            FullName = model.FullName.Trim(),
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "User");
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [Route("sifremi-unuttum")]
    [Route("Account/ForgotPassword")]
    public IActionResult ForgotPassword(string? method = "email")
    {
        return View(new ForgotPasswordViewModel { Method = (method == "phone" ? "phone" : "email") });
    }

    [HttpPost]
    [Route("sifremi-unuttum")]
    [Route("Account/ForgotPassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model, 
        [FromForm(Name = "cf-turnstile-response")] string? turnstileToken = null)
    {
        var clientIp = GetClientIp();

        if (model.Method == "phone")
        {
            // Phone SMS Reset Flow
            if (model.Step == 1)
            {
                // Rate Limiting (IP başına 15 dakikada en fazla 5 talep)
                var (rlAllowed, rlError) = _rateLimitService.CheckRateLimit("forgot_password", clientIp, 5, TimeSpan.FromMinutes(15));
                if (!rlAllowed)
                {
                    ModelState.AddModelError("", rlError ?? "Çok fazla şifre sıfırlama talebi gönderildi. Lütfen bekleyiniz.");
                    return View(model);
                }

                // CAPTCHA Doğrulaması
                var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, "forgot_password_phone", clientIp);
                if (!captchaValid)
                {
                    ModelState.AddModelError("", captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.");
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Lütfen cep telefonu numaranızı giriniz.");
                    return View(model);
                }

                var (reqSuccess, reqError, session) = await _phoneResetService.RequestCodeAsync(model.PhoneNumber);
                if (!reqSuccess)
                {
                    ModelState.AddModelError("", reqError ?? "SMS gönderilemedi.");
                    return View(model);
                }

                model.Step = 2;
                model.SessionToken = session?.SessionToken;
                model.MaskedPhone = session?.MaskedPhone ?? PhoneHelper.Mask(model.PhoneNumber);
                model.RemainingCooldownSeconds = 60;
                ViewBag.PhoneCodeSent = true;
                return View(model);
            }
            else if (model.Step == 2)
            {
                if (string.IsNullOrWhiteSpace(model.SmsCode))
                {
                    ModelState.AddModelError("SmsCode", "Lütfen telefonunuza gelen 6 haneli doğrulama kodunu giriniz.");
                    return View(model);
                }

                var (verifySuccess, verifyError) = await _phoneResetService.VerifyCodeAsync(model.SessionToken ?? "", model.SmsCode);
                if (!verifySuccess)
                {
                    ModelState.AddModelError("SmsCode", verifyError ?? "Hatalı doğrulama kodu.");
                    return View(model);
                }

                // Kod başarıyla doğrulandı -> Yeni şifre alanlarını aç
                model.Step = 3;
                return View(model);
            }
            else if (model.Step == 3)
            {
                if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
                {
                    ModelState.AddModelError("NewPassword", "Yeni şifre en az 6 karakter olmalıdır.");
                    return View(model);
                }

                if (model.NewPassword != model.ConfirmNewPassword)
                {
                    ModelState.AddModelError("ConfirmNewPassword", "Girdiğiniz şifreler birbiriyle uyuşmuyor.");
                    return View(model);
                }

                var (resetSuccess, resetError, user, token) = await _phoneResetService.CompleteResetAsync(model.SessionToken ?? "", model.NewPassword);
                if (!resetSuccess)
                {
                    ModelState.AddModelError("", resetError ?? "Şifre yenilenemedi.");
                    return View(model);
                }

                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            return View(model);
        }
        else
        {
            // Email Reset Flow
            // Rate Limiting (IP başına 15 dakikada en fazla 5 talep)
            var (rlAllowed, rlError) = _rateLimitService.CheckRateLimit("forgot_password", clientIp, 5, TimeSpan.FromMinutes(15));
            if (!rlAllowed)
            {
                ModelState.AddModelError("", rlError ?? "Çok fazla şifre sıfırlama talebi gönderildi. Lütfen bekleyiniz.");
                return View(model);
            }

            // CAPTCHA Doğrulaması
            var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, "forgot_password_email", clientIp);
            if (!captchaValid)
            {
                ModelState.AddModelError("", captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("Email", "Lütfen e-posta adresinizi giriniz.");
                return View(model);
            }

            var cleanEmail = model.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(cleanEmail);

            // Güvenlik: Kullanıcının var olup olmadığını açığa çıkarma
            if (user != null && !user.IsBlocked)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink = Url.Action("ResetPassword", "Account", new { email = user.Email, token = token }, Request.Scheme);

                var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                    <div style='background: #030f27; padding: 15px; text-align: center; border-radius: 6px 6px 0 0;'>
                        <h2 style='color: #fdbe33; margin: 0;'>Şen Yapı İnşaat A.Ş.</h2>
                    </div>
                    <div style='padding: 20px;'>
                        <p>Merhaba <strong>{user.FullName ?? user.UserName}</strong>,</p>
                        <p>Şen Yapı hesabınız için bir şifre sıfırlama talebinde bulunuldu. Şifrenizi yenilemek için lütfen aşağıdaki bağlantıya tıklayın:</p>
                        <p style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' style='background: #fdbe33; color: #030f27; padding: 12px 24px; text-decoration: none; font-weight: bold; border-radius: 5px; display: inline-block;'>Yeni Şifre Belirle</a>
                        </p>
                        <p style='color: #666; font-size: 13px;'>Bu bağlantı süreli ve tek kullanımlıktır. Eğer bu talebi siz yapmadıysanız lütfen bu e-postayı dikkate almayınız.</p>
                    </div>
                </div>";

                await _emailService.SendEmailAsync(user.Email!, "Şifre Sıfırlama Talebi - Şen Yapı", emailBody);
            }

            ViewBag.MessageSent = true;
            return View(model);
        }
    }

    [HttpPost]
    [Route("Account/ResendSmsCode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendSmsCode(string sessionToken)
    {
        var (success, error, remainingCooldown) = await _phoneResetService.ResendCodeAsync(sessionToken);
        return Json(new { success, message = error ?? "Yeni doğrulama kodu SMS ile gönderildi.", remainingCooldown });
    }

    [HttpGet]
    [Route("yeni-sifre-belirle")]
    [Route("Account/ResetPassword")]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction("Login");
        }

        var model = new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        };
        return View(model);
    }

    [HttpPost]
    [Route("yeni-sifre-belirle")]
    [Route("Account/ResetPassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Do not disclose user nonexistence
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            // Also reset any lockouts
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [Route("sifre-sifirlandi")]
    [Route("Account/ResetPasswordConfirmation")]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    [HttpPost]
    [Route("cikis-yap")]
    [Route("Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Route("cikis-yap")]
    [Route("Account/Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Route("Account/AccessDenied")]
    [Route("erisim-engellendi")]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
