using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;

namespace SenYapi.Web.Controllers;

public class NewsletterController : Controller
{
    private readonly INewsletterService _newsletterService;

    public NewsletterController(INewsletterService newsletterService)
    {
        _newsletterService = newsletterService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["NewsletterError"] = "Lütfen geçerli bir e-posta adresi giriniz.";
        }
        else
        {
            var success = await _newsletterService.SubscribeAsync(email);
            if (success)
            {
                TempData["NewsletterSuccess"] = "E-bülten kaydınız başarıyla tamamlandı. Teşekkür ederiz!";
            }
            else
            {
                TempData["NewsletterError"] = "Kayıt sırasında bir hata oluştu.";
            }
        }

        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer))
        {
            return Redirect(referer);
        }

        return RedirectToAction("Index", "Home");
    }
}
