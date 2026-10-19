using System.ComponentModel.DataAnnotations;

namespace SenYapi.Business.DTOs;

public class BlogCommentDto
{
    public int BlogPostId { get; set; }

    [Required(ErrorMessage = "Lütfen adınızı giriniz.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = string.Empty;

    public string? Website { get; set; }

    [Required(ErrorMessage = "Lütfen yorumunuzu yazınız.")]
    public string Comment { get; set; } = string.Empty;
}
