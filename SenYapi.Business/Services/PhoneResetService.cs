using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class PhoneResetService : IPhoneResetService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ISmsService _smsService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<PhoneResetService> _logger;

    private const int CodeValidityMinutes = 3;
    private const int ResendCooldownSeconds = 60;
    private const int MaxFailedAttempts = 3;

    public PhoneResetService(
        UserManager<AppUser> userManager,
        ISmsService smsService,
        IMemoryCache memoryCache,
        ILogger<PhoneResetService> logger)
    {
        _userManager = userManager;
        _smsService = smsService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, PhoneResetSession? Session)> RequestCodeAsync(string rawPhoneNumber)
    {
        var normalizedPhone = PhoneHelper.Normalize(rawPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return (false, "Lütfen geçerli bir cep telefonu numarası giriniz (örn: 0532 123 4567).", null);
        }

        // SMS ayarları girilmemişse kullanıcıya açık hata göster ve "SMS gönderildi" deme
        if (!_smsService.IsConfigured)
        {
            var err = "SMS servisi yapılandırılmamış. Lütfen appsettings.json dosyasından SMS sağlayıcı bilgilerinizi tanımlayınız veya e-posta ile şifre sıfırlama seçeneğini kullanınız.";
            _logger.LogWarning("SMS servisi yapılandırılmadığı için şifre sıfırlama kodu gönderilemedi. Girilen numara: {Phone}", PhoneHelper.Mask(normalizedPhone));
            return (false, err, null);
        }

        // Hesaba önceden kaydedilmiş ve doğrulanmış (PhoneNumberConfirmed == true) telefon numaralarını sorgula
        var confirmedUsers = await _userManager.Users
            .Where(u => u.PhoneNumberConfirmed && u.PhoneNumber != null && !u.IsBlocked)
            .ToListAsync();

        var user = confirmedUsers.FirstOrDefault(u => PhoneHelper.IsMatch(u.PhoneNumber, normalizedPhone));

        var sessionToken = Guid.NewGuid().ToString("N");

        // Kullanıcı bulunamadıysa veya telefon onaylı değilse:
        // Hesap bulunup bulunmadığını açığa çıkarmayan bilgilendirme mesajı (User enumeration prevention)
        if (user == null)
        {
            var dummySession = new PhoneResetSession
            {
                SessionToken = sessionToken,
                UserId = null,
                NormalizedPhone = normalizedPhone,
                MaskedPhone = PhoneHelper.Mask(normalizedPhone),
                VerificationCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(CodeValidityMinutes),
                ResendCooldownUntil = DateTime.UtcNow.AddSeconds(ResendCooldownSeconds),
                FailedAttempts = 0,
                IsVerified = false,
                UserFound = false
            };
            _memoryCache.Set(sessionToken, dummySession, TimeSpan.FromMinutes(CodeValidityMinutes + 2));
            _logger.LogInformation("Şifre sıfırlama için doğrulanmış telefon bulunamadı. Genel mesaj dönüldü: {Phone}", PhoneHelper.Mask(normalizedPhone));
            return (true, null, dummySession);
        }

        // 6 haneli güvenli doğrulama kodu üret
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var smsMessage = $"Şen Yapı şifre sıfırlama doğrulama kodunuz: {code}. Bu kod {CodeValidityMinutes} dakika süreyle geçerlidir.";

        var (smsSuccess, smsError) = await _smsService.SendSmsAsync(normalizedPhone, smsMessage);
        if (!smsSuccess)
        {
            // SMS entegrasyonu yokken veya gönderim başarısızken açık hata göster
            return (false, smsError ?? "SMS gönderimi başarısız oldu. Lütfen sistem yöneticisiyle iletişime geçiniz.", null);
        }

        var session = new PhoneResetSession
        {
            SessionToken = sessionToken,
            UserId = user.Id,
            NormalizedPhone = normalizedPhone,
            MaskedPhone = PhoneHelper.Mask(normalizedPhone),
            VerificationCode = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(CodeValidityMinutes),
            ResendCooldownUntil = DateTime.UtcNow.AddSeconds(ResendCooldownSeconds),
            FailedAttempts = 0,
            IsVerified = false,
            UserFound = true
        };

        _memoryCache.Set(sessionToken, session, TimeSpan.FromMinutes(CodeValidityMinutes + 2));
        _logger.LogInformation("Şifre sıfırlama kodu SMS ile başarıyla iletildi: {Phone}", PhoneHelper.Mask(normalizedPhone));
        return (true, null, session);
    }

    public Task<(bool Success, string? ErrorMessage)> VerifyCodeAsync(string sessionToken, string inputCode)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || !_memoryCache.TryGetValue(sessionToken, out PhoneResetSession? session) || session == null)
        {
            return Task.FromResult((false, (string?)"Doğrulama oturumunun süresi dolmuş veya geçersiz. Lütfen baştan başlayınız."));
        }

        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _memoryCache.Remove(sessionToken);
            return Task.FromResult((false, (string?)$"Doğrulama kodunun {CodeValidityMinutes} dakikalık geçerlilik süresi dolmuştur. Lütfen yeni bir kod isteyiniz."));
        }

        var cleanCode = inputCode?.Trim();
        if (string.IsNullOrWhiteSpace(cleanCode))
        {
            return Task.FromResult((false, (string?)"Lütfen 6 haneli doğrulama kodunu giriniz."));
        }

        // Sahte oturum veya hatalı kod durumunda
        if (!session.UserFound || !string.Equals(session.VerificationCode, cleanCode, StringComparison.Ordinal))
        {
            session.FailedAttempts++;
            if (session.FailedAttempts >= MaxFailedAttempts)
            {
                _memoryCache.Remove(sessionToken);
                return Task.FromResult((false, (string?)$"{MaxFailedAttempts} kez hatalı kod girdiniz. Güvenliğiniz için bu doğrulama oturumu sonlandırıldı. Lütfen baştan başlayınız."));
            }

            var remaining = MaxFailedAttempts - session.FailedAttempts;
            _memoryCache.Set(sessionToken, session, TimeSpan.FromMinutes(CodeValidityMinutes));
            return Task.FromResult((false, (string?)$"Girdiğiniz doğrulama kodu hatalıdır. Kalan deneme hakkınız: {remaining}"));
        }

        // Kod başarılı şekilde doğrulandı
        session.IsVerified = true;
        _memoryCache.Set(sessionToken, session, TimeSpan.FromMinutes(5));
        return Task.FromResult((true, (string?)null));
    }

    public async Task<(bool Success, string? ErrorMessage, AppUser? User, string? ResetToken)> CompleteResetAsync(string sessionToken, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || !_memoryCache.TryGetValue(sessionToken, out PhoneResetSession? session) || session == null)
        {
            return (false, "Doğrulama oturumu bulunamadı veya süresi doldu.", null, null);
        }

        if (!session.IsVerified || !session.UserFound || string.IsNullOrEmpty(session.UserId))
        {
            return (false, "Doğrulama kodu henüz doğrulanmadı. Lütfen önce telefonunuza gelen kodu onaylayınız.", null, null);
        }

        var user = await _userManager.FindByIdAsync(session.UserId);
        if (user == null || user.IsBlocked)
        {
            _memoryCache.Remove(sessionToken);
            return (false, "Kullanıcı hesabı bulunamadı veya engellendi.", null, null);
        }

        // ASP.NET Core Identity mekanizması ile sıfırlama token'ı oluştur
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            // Olası hesap kilitlenmelerini ve hatalı sayaçları sıfırla
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            _memoryCache.Remove(sessionToken);
            _logger.LogInformation("Kullanıcı şifresi telefon doğrulaması ile başarıyla yenilendi. Kullanıcı: {UserName}", user.UserName);
            return (true, null, user, token);
        }

        var errorMsg = string.Join(" ", result.Errors.Select(e => e.Description));
        return (false, errorMsg, user, null);
    }

    public async Task<(bool Success, string? ErrorMessage, int RemainingCooldownSeconds)> ResendCodeAsync(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || !_memoryCache.TryGetValue(sessionToken, out PhoneResetSession? session) || session == null)
        {
            return (false, "Oturum süresi dolmuş. Lütfen telefon numaranızı tekrar giriniz.", 0);
        }

        var remainingSeconds = (int)Math.Ceiling((session.ResendCooldownUntil - DateTime.UtcNow).TotalSeconds);
        if (remainingSeconds > 0)
        {
            return (false, $"Yeni bir kod talep edebilmek için lütfen {remainingSeconds} saniye bekleyiniz.", remainingSeconds);
        }

        var newCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        session.VerificationCode = newCode;
        session.ExpiresAt = DateTime.UtcNow.AddMinutes(CodeValidityMinutes);
        session.ResendCooldownUntil = DateTime.UtcNow.AddSeconds(ResendCooldownSeconds);
        session.FailedAttempts = 0;

        if (session.UserFound)
        {
            var smsMessage = $"Şen Yapı şifre sıfırlama doğrulama kodunuz: {newCode}. Bu kod {CodeValidityMinutes} dakika süreyle geçerlidir.";
            var (smsSuccess, smsError) = await _smsService.SendSmsAsync(session.NormalizedPhone, smsMessage);
            if (!smsSuccess)
            {
                return (false, smsError ?? "SMS gönderilemedi.", 0);
            }
        }

        _memoryCache.Set(sessionToken, session, TimeSpan.FromMinutes(CodeValidityMinutes + 2));
        return (true, null, ResendCooldownSeconds);
    }

    public PhoneResetSession? GetSession(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken)) return null;
        _memoryCache.TryGetValue(sessionToken, out PhoneResetSession? session);
        return session;
    }
}
