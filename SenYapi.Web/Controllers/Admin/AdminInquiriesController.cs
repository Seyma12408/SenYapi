using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
public class AdminInquiriesController : AdminBaseController
{
    private readonly IProjectInquiryService _inquiryService;
    private readonly IAuditLogService _auditLogService;

    public AdminInquiriesController(
        IProjectInquiryService inquiryService,
        IAuditLogService auditLogService)
    {
        _inquiryService = inquiryService;
        _auditLogService = auditLogService;
    }

    [HttpGet("Inquiries")]
    public async Task<IActionResult> Inquiries(string? status)
    {
        var inquiries = await _inquiryService.GetAllInquiriesAsync();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            inquiries = inquiries.Where(i => i.Status == status);
        }

        ViewBag.Status = status;
        return View("~/Views/Admin/Inquiries.cshtml", inquiries.ToList());
    }

    [HttpPost("ReplyInquiry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyInquiry(int id, string replyText, string newStatus)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            TempData["AdminError"] = "Cevap metni boş olamaz.";
            return RedirectToAction(nameof(Inquiries));
        }

        if (!ValidationHelper.IsValidInquiryStatus(newStatus))
        {
            TempData["AdminError"] = "Geçersiz bilgi talebi durumu seçildi.";
            return RedirectToAction(nameof(Inquiries));
        }

        var success = await _inquiryService.ReplyInquiryAsync(id, replyText.Trim(), newStatus, CurrentAdminName);
        if (success)
        {
            TempData["AdminSuccess"] = "Bilgi talebine yanıt gönderildi.";
            await _auditLogService.LogAsync(CurrentAdminName, "Bilgi Talebi Yanıtlama", "ProjectInquiry", $"InquiryId: {id}, Durum: {newStatus}");
        }
        else
        {
            TempData["AdminError"] = "Bilgi talebi bulunamadı.";
        }
        return RedirectToAction(nameof(Inquiries));
    }

    [HttpPost("UpdateInquiryStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInquiryStatus(int id, string status)
    {
        if (!ValidationHelper.IsValidInquiryStatus(status))
        {
            TempData["AdminError"] = "Geçersiz bilgi talebi durumu seçildi.";
            return RedirectToAction(nameof(Inquiries));
        }

        await _inquiryService.UpdateInquiryStatusAsync(id, status);
        TempData["AdminSuccess"] = "Bilgi talebi durumu güncellendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Bilgi Talebi Durum Güncelleme", "ProjectInquiry", $"InquiryId: {id}, Durum: {status}");
        return RedirectToAction(nameof(Inquiries));
    }
}
