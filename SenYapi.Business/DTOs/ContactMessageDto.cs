using System.ComponentModel.DataAnnotations;

namespace SenYapi.Business.DTOs;

public class ContactMessageDto
{
    [Required(ErrorMessage = "Lütfen adınızı ve soyadınızı giriniz.")]
    [StringLength(100, ErrorMessage = "Adınız en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen konu başlığını giriniz.")]
    [StringLength(150, ErrorMessage = "Konu en fazla 150 karakter olabilir.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen mesajınızı yazınız.")]
    [StringLength(2000, ErrorMessage = "Mesaj en fazla 2000 karakter olabilir.")]
    public string Message { get; set; } = string.Empty;

    public string? UserId { get; set; }
}
