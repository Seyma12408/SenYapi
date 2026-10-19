using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Core.Constants;
using SenYapi.Web.Filters;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
public class AdminCommentsController : AdminBaseController
{
    private readonly IBlogService _blogService;
    private readonly IAuditLogService _auditLogService;

    public AdminCommentsController(
        IBlogService blogService,
        IAuditLogService auditLogService)
    {
        _blogService = blogService;
        _auditLogService = auditLogService;
    }

    [HttpGet("Comments")]
    [HasPermission(Permissions.CommentView)]
    public async Task<IActionResult> Comments(string? status)
    {
        var comments = await _blogService.GetAllCommentsAsync();

        if (status == "pending")
            comments = comments.Where(c => !c.IsApproved);
        else if (status == "approved")
            comments = comments.Where(c => c.IsApproved);

        ViewBag.Status = status;
        return View("~/Views/Admin/Comments.cshtml", comments.ToList());
    }

    [HttpPost("ApproveComment")]
    [HttpPost("ApproveComment/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.CommentApprove)]
    public async Task<IActionResult> ApproveComment(int id)
    {
        await _blogService.ApproveCommentAsync(id);
        TempData["AdminSuccess"] = "Yorum onaylandı ve yayına alındı.";
        await _auditLogService.LogAsync(CurrentAdminName, "Yorum Onaylama", "BlogComment", $"CommentId: {id}");
        return RedirectToAction(nameof(Comments));
    }

    [HttpPost("RejectComment")]
    [HttpPost("RejectComment/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.CommentApprove)]
    public async Task<IActionResult> RejectComment(int id)
    {
        await _blogService.RejectCommentAsync(id);
        TempData["AdminSuccess"] = "Yorum onayı kaldırıldı.";
        await _auditLogService.LogAsync(CurrentAdminName, "Yorum Reddetme", "BlogComment", $"CommentId: {id}");
        return RedirectToAction(nameof(Comments));
    }

    [HttpPost("ReplyComment")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.CommentReply)]
    public async Task<IActionResult> ReplyComment(int id, string replyText)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            TempData["AdminError"] = "Cevap metni boş olamaz.";
            return RedirectToAction(nameof(Comments));
        }

        await _blogService.ReplyCommentAsync(id, replyText.Trim());
        TempData["AdminSuccess"] = "Yorum yanıtlandı.";
        await _auditLogService.LogAsync(CurrentAdminName, "Yorum Yanıtlama", "BlogComment", $"CommentId: {id}");
        return RedirectToAction(nameof(Comments));
    }

    [HttpPost("DeleteComment")]
    [HttpPost("DeleteComment/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.CommentDelete)]
    public async Task<IActionResult> DeleteComment(int id)
    {
        await _blogService.DeleteCommentAsync(id);
        TempData["AdminSuccess"] = "Yorum silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Yorum Silme", "BlogComment", $"CommentId: {id}");
        return RedirectToAction(nameof(Comments));
    }
}
