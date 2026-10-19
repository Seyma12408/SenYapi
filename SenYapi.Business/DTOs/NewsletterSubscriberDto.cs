using System.ComponentModel.DataAnnotations;

namespace SenYapi.Business.DTOs;

public class NewsletterSubscriberDto
{
    [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = string.Empty;
}
