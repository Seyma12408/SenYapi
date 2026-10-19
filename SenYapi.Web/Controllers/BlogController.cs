using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Entity;

namespace SenYapi.Web.Controllers;

public class BlogController : Controller
{
    private readonly IBlogService _blogService;
    private readonly IUrlRedirectService _redirectService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ICaptchaService _captchaService;
    private readonly IRateLimitService _rateLimitService;

    public BlogController(
        IBlogService blogService,
        IUrlRedirectService redirectService,
        UserManager<AppUser> userManager,
        ICaptchaService captchaService,
        IRateLimitService rateLimitService)
    {
        _blogService = blogService;
        _redirectService = redirectService;
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
    [Route("blog")]
    [Route("Blog/Index")]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 6;
        var allPosts = (await _blogService.GetAllPostsAsync(onlyPublished: true)).ToList();
        var totalCount = allPosts.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var pagedPosts = allPosts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(pagedPosts);
    }

    [HttpGet]
    [Route("blog/{slug}")]
    [Route("Blog/Detail/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var cleanSlug = slug.Trim().ToLowerInvariant();

        // 1. Try find by slug first: if active published post exists, show it immediately
        var post = await _blogService.GetPostBySlugAsync(cleanSlug);
        if (post != null && post.IsPublished)
        {
            await _blogService.IncrementViewCountAsync(post.Id);
            return View(post);
        }

        // 2. If post not found directly, check 301 Redirects (e.g. slug changed from old to new)
        var currentPath = $"/blog/{cleanSlug}";
        var redirectUrl = await _redirectService.CheckRedirectAsync(currentPath);
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            return RedirectPermanent(redirectUrl);
        }

        // 3. Fallback: if slug is numeric id
        if (int.TryParse(cleanSlug, out int id))
        {
            var postById = await _blogService.GetPostByIdAsync(id);
            if (postById != null && !string.IsNullOrEmpty(postById.Slug) && postById.IsPublished)
            {
                return RedirectPermanent($"/blog/{postById.Slug}");
            }
        }

        return NotFound();
    }

    [HttpPost]
    [Route("Blog/AddComment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        int blogPostId, 
        string? name, 
        string? email, 
        string? website, 
        string comment, 
        string returnSlug,
        int? parentCommentId = null,
        [FromForm(Name = "cf-turnstile-response")] string? turnstileToken = null)
    {
        var clientIp = GetClientIp();

        // 1. Rate Limiting (IP başına 10 dakikada en fazla 10 yorum/yanıt)
        var (rlAllowed, rlError) = _rateLimitService.CheckRateLimit("blog_comment", clientIp, 10, TimeSpan.FromMinutes(10));
        if (!rlAllowed)
        {
            TempData["CommentError"] = rlError ?? "Çok fazla yorum gönderildi. Lütfen biraz bekleyiniz.";
            TempData["PreservedComment"] = comment;
            TempData["PreservedName"] = name;
            TempData["PreservedEmail"] = email;
            TempData["PreservedWebsite"] = website;
            return Redirect($"/blog/{returnSlug}#yorum-formu");
        }

        // 2. CAPTCHA Doğrulaması
        var expectedAction = (parentCommentId.HasValue && parentCommentId.Value > 0) ? "blog_reply" : "blog_comment";
        var (captchaValid, captchaError) = await _captchaService.VerifyTokenAsync(turnstileToken, expectedAction, clientIp);
        if (!captchaValid)
        {
            TempData["CommentError"] = captchaError ?? "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.";
            TempData["PreservedComment"] = comment;
            TempData["PreservedName"] = name;
            TempData["PreservedEmail"] = email;
            TempData["PreservedWebsite"] = website;
            return Redirect($"/blog/{returnSlug}#yorum-formu");
        }

        if (string.IsNullOrWhiteSpace(comment) || comment.Length < 3 || comment.Length > 2000)
        {
            TempData["CommentError"] = "Yorum 3 ile 2000 karakter arasında olmalıdır.";
            TempData["PreservedComment"] = comment;
            TempData["PreservedName"] = name;
            TempData["PreservedEmail"] = email;
            TempData["PreservedWebsite"] = website;
            return Redirect($"/blog/{returnSlug}#yorum-formu");
        }

        string userId = null!;
        string commenterName = name?.Trim() ?? string.Empty;
        string commenterEmail = email?.Trim() ?? string.Empty;
        bool isStaff = false;
        string? userRole = null;

        var post = await _blogService.GetPostByIdAsync(blogPostId);

        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                userId = currentUser.Id;
                commenterName = !string.IsNullOrWhiteSpace(currentUser.FullName) ? currentUser.FullName : currentUser.UserName!;
                commenterEmail = currentUser.Email ?? commenterEmail;

                if (User.IsInRole("SystemAdmin"))
                {
                    isStaff = true;
                    userRole = "Sistem Yöneticisi";
                }
                else if (User.IsInRole("Admin"))
                {
                    isStaff = true;
                    userRole = "Yönetici";
                }
                else if (User.IsInRole("Editor"))
                {
                    isStaff = true;
                    userRole = "Editör";
                }
                else if (post != null && (!string.IsNullOrWhiteSpace(post.Author) && 
                    (string.Equals(post.Author, currentUser.FullName, StringComparison.OrdinalIgnoreCase) || 
                     string.Equals(post.Author, currentUser.UserName, StringComparison.OrdinalIgnoreCase))))
                {
                    isStaff = true;
                    userRole = "Yazar";
                }
                else
                {
                    userRole = "Üye";
                }
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(commenterName) || string.IsNullOrWhiteSpace(commenterEmail))
            {
                TempData["CommentError"] = "Lütfen adınızı ve geçerli bir e-posta adresini giriniz.";
                TempData["PreservedComment"] = comment;
                TempData["PreservedName"] = name;
                TempData["PreservedEmail"] = email;
                TempData["PreservedWebsite"] = website;
                return Redirect($"/blog/{returnSlug}#yorum-formu");
            }
        }

        var cleanComment = comment.Trim();

        // Spam ve art arda hızlı çoklu gönderim engeli (Son 15 saniyede aynı yorum kontrolü)
        if (post != null && post.Comments != null)
        {
            var isRecentDuplicate = post.Comments.Any(c =>
                c.Comment == cleanComment &&
                (c.UserId == userId || c.Email.Equals(commenterEmail, StringComparison.OrdinalIgnoreCase)) &&
                c.CreatedAt > DateTime.UtcNow.AddSeconds(-15));

            if (isRecentDuplicate)
            {
                TempData["CommentError"] = "Bu yorum az önce gönderildi. Lütfen mükerrer gönderimleri önlemek için biraz bekleyin.";
                return Redirect($"/blog/{returnSlug}#yorum-alani");
            }
        }

        var newComment = new BlogComment
        {
            BlogPostId = blogPostId,
            UserId = userId,
            Name = commenterName,
            Email = commenterEmail,
            Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim(),
            Comment = cleanComment,
            ParentCommentId = (parentCommentId.HasValue && parentCommentId.Value > 0) ? parentCommentId : null,
            IsStaffReply = isStaff,
            UserRole = userRole,
            IsApproved = isStaff, // Yetkili ve yazar yanıtları anında yayına alınır
            IsDeleted = false
        };

        await _blogService.AddCommentAsync(newComment);
        TempData["CommentSuccess"] = isStaff 
            ? "Yanıtınız başarıyla yayımlandı." 
            : "Yorumunuz alındı, yönetici onayından sonra yayımlanacaktır.";

        return Redirect($"/blog/{returnSlug}#yorum-alani");
    }

    [HttpPost]
    [Route("Blog/EditMyComment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMyComment(int commentId, string updatedComment, string returnSlug)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var comment = await _blogService.GetCommentByIdAsync(commentId);
        if (comment == null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (comment.UserId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(updatedComment) || updatedComment.Length < 3)
        {
            TempData["CommentError"] = "Yorum metni en az 3 karakter olmalıdır.";
            return Redirect($"/blog/{returnSlug}#yorum-alani");
        }

        comment.Comment = updatedComment.Trim();
        await _blogService.UpdateCommentAsync(comment);

        TempData["CommentSuccess"] = "Yorumunuz güncellendi ve yeniden yönetici onayına sunuldu.";
        return Redirect($"/blog/{returnSlug}#yorum-alani");
    }

    [HttpPost]
    [Route("Blog/DeleteMyComment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMyComment(int commentId, string returnSlug)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _blogService.DeleteUserCommentAsync(commentId, currentUserId!);

        TempData["CommentSuccess"] = "Yorumunuz silindi.";
        return Redirect($"/blog/{returnSlug}#yorum-alani");
    }
}
