using System.ComponentModel.DataAnnotations;

namespace SenYapi.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Lütfen kullanıcı adınızı veya e-posta adresinizi giriniz.")]
    [Display(Name = "Kullanıcı Adı veya E-Posta")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen şifrenizi giriniz.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni Hatırla")]
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Lütfen adınızı ve soyadınızı giriniz.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen kullanıcı adınızı giriniz.")]
    [RegularExpression(@"^[a-zA-Z0-9_-]{3,20}$", ErrorMessage = "Kullanıcı adı 3-20 karakter arasında olmalı ve yalnızca harf, rakam, alt tire veya tire içermelidir.")]
    [Display(Name = "Kullanıcı Adı")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "E-Posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen şifrenizi giriniz.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Şifreler uyuşmuyor.")]
    [Display(Name = "Şifre Tekrar")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    // Yöntem seçimi: "email" veya "phone"
    public string Method { get; set; } = "email";

    // E-posta ile sıfırlama
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "E-Posta Adresi")]
    public string? Email { get; set; }

    // Telefon ile sıfırlama
    [Display(Name = "Cep Telefonu Numarası")]
    public string? PhoneNumber { get; set; }

    // SMS Doğrulama Aşaması
    public int Step { get; set; } = 1; // 1 = Telefon Gir, 2 = Kodu Gir, 3 = Yeni Şifre Belirle
    public string? SessionToken { get; set; }
    public string? MaskedPhone { get; set; }

    [Display(Name = "6 Haneli Doğrulama Kodu")]
    public string? SmsCode { get; set; }

    // Yeni Şifre Aşaması (Kod doğrulandıktan sonra açılır)
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Şifre")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Şifreler uyuşmuyor.")]
    [Display(Name = "Yeni Şifre Tekrar")]
    public string? ConfirmNewPassword { get; set; }

    public int RemainingCooldownSeconds { get; set; } = 0;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen yeni şifrenizi giriniz.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Şifre")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Şifreler uyuşmuyor.")]
    [Display(Name = "Yeni Şifre Tekrar")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
