using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Entity;

using SenYapi.Core.Constants;
using SenYapi.Web.Filters;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
public class AdminBlogController : AdminBaseController
{
    private readonly IBlogService _blogService;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUrlRedirectService _redirectService;

    public AdminBlogController(
        IBlogService blogService,
        IFileService fileService,
        IAuditLogService auditLogService,
        IUrlRedirectService redirectService)
    {
        _blogService = blogService;
        _fileService = fileService;
        _auditLogService = auditLogService;
        _redirectService = redirectService;
    }

    [HttpGet("Blog")]
    [HasPermission(Permissions.BlogView)]
    public async Task<IActionResult> Blog(string? search, string? category)
    {
        var posts = await _blogService.GetAllPostsAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim().ToLowerInvariant();
            posts = posts.Where(p =>
                p.Title.ToLower().Contains(cleanSearch) ||
                (p.Summary != null && p.Summary.ToLower().Contains(cleanSearch)) ||
                (p.Content != null && p.Content.ToLower().Contains(cleanSearch)));
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
        {
            posts = posts.Where(p => p.Category == category);
        }

        ViewBag.Search = search;
        ViewBag.Category = category;

        return View("~/Views/Admin/Blog.cshtml", posts.ToList());
    }

    [HttpPost("AddPost")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.BlogCreate)]
    public async Task<IActionResult> AddPost(BlogPost model, IFormFile? imageFile)
    {
        if (!ValidationHelper.IsCleanText(model.Title, 2, 200))
        {
            var posts = await _blogService.GetAllPostsAsync();
            ViewBag.FailedModalId = "addPostModal";
            ViewBag.SlugError = "Lütfen en az 2, en fazla 200 karakterden oluşan geçerli bir başlık giriniz.";
            ViewBag.PreservedModel = model;
            return View("~/Views/Admin/Blog.cshtml", posts.ToList());
        }

        if (!ValidationHelper.IsCleanText(model.Content, 5, 100000))
        {
            var posts = await _blogService.GetAllPostsAsync();
            ViewBag.FailedModalId = "addPostModal";
            ViewBag.SlugError = "Lütfen geçerli ve en az 5 karakterden oluşan blog içeriği giriniz.";
            ViewBag.PreservedModel = model;
            return View("~/Views/Admin/Blog.cshtml", posts.ToList());
        }

        var rawSlug = !string.IsNullOrWhiteSpace(model.Slug) ? model.Slug : model.Title;
        var cleanSlug = SlugHelper.GenerateSlug(rawSlug);
        if (string.IsNullOrWhiteSpace(cleanSlug))
        {
            cleanSlug = "yazi";
        }

        if (await _blogService.IsSlugInUseAsync(cleanSlug, 0))
        {
            var posts = await _blogService.GetAllPostsAsync();
            ViewBag.FailedModalId = "addPostModal";
            ViewBag.SlugError = "Bu adres zaten kullanılıyor. Lütfen farklı bir adres girin.";
            ViewBag.PreservedModel = model;
            return View("~/Views/Admin/Blog.cshtml", posts.ToList());
        }

        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "blog");
            if (success)
            {
                model.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Blog));
            }
        }
        else if (string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            model.ImageUrl = "~/theme/builderz/img/blog-1.jpg";
        }

        model.Author = !string.IsNullOrWhiteSpace(model.Author) ? model.Author : CurrentAdminName;
        model.Slug = cleanSlug;

        await _blogService.CreatePostAsync(model, CurrentAdminName);
        TempData["AdminSuccess"] = $"'{model.Title}' başlıklı blog yazısı başarıyla eklendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Ekleme", "BlogPost", $"Yazı: {model.Title}");
        return RedirectToAction(nameof(Blog));
    }

    [HttpPost("UpdatePost")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.BlogEdit)]
    public async Task<IActionResult> UpdatePost(BlogPost model, IFormFile? imageFile)
    {
        var existing = await _blogService.GetPostByIdAsync(model.Id);
        if (existing == null)
        {
            TempData["AdminError"] = "Güncellenecek blog yazısı bulunamadı.";
            return RedirectToAction(nameof(Blog));
        }

        if (!ValidationHelper.IsCleanText(model.Title, 2, 200))
        {
            var posts = await _blogService.GetAllPostsAsync();
            ViewBag.FailedModalId = $"blogEditModal-{model.Id}";
            ViewBag.SlugError = "Lütfen en az 2, en fazla 200 karakterden oluşan geçerli bir başlık giriniz.";
            ViewBag.PreservedModel = model;
            return View("~/Views/Admin/Blog.cshtml", posts.ToList());
        }

        if (!ValidationHelper.IsCleanText(model.Content, 5, 100000))
        {
            var posts = await _blogService.GetAllPostsAsync();
            ViewBag.FailedModalId = $"blogEditModal-{model.Id}";
            ViewBag.SlugError = "Lütfen geçerli ve en az 5 karakterden oluşan blog içeriği giriniz.";
            ViewBag.PreservedModel = model;
            return View("~/Views/Admin/Blog.cshtml", posts.ToList());
        }

        var oldSlug = existing.Slug;
        var rawSlug = !string.IsNullOrWhiteSpace(model.Slug) ? model.Slug : model.Title;
        var cleanSlug = SlugHelper.GenerateSlug(rawSlug);
        if (string.IsNullOrWhiteSpace(cleanSlug))
        {
            cleanSlug = "yazi";
        }

        if (await _blogService.IsSlugInUseAsync(cleanSlug, existing.Id))
        {
            var posts = await _blogService.GetAllPostsAsync();
            ViewBag.FailedModalId = $"blogEditModal-{model.Id}";
            ViewBag.SlugError = "Bu adres zaten kullanılıyor. Lütfen farklı bir adres girin.";
            ViewBag.PreservedModel = model;
            return View("~/Views/Admin/Blog.cshtml", posts.ToList());
        }

        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "blog");
            if (success)
            {
                existing.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Blog));
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            existing.ImageUrl = model.ImageUrl;
        }

        if (!string.Equals(oldSlug, cleanSlug, StringComparison.OrdinalIgnoreCase))
        {
            var isOldSlugOwnedByOther = await _blogService.IsSlugInUseAsync(oldSlug, existing.Id);
            if (!isOldSlugOwnedByOther && !string.IsNullOrWhiteSpace(oldSlug))
            {
                var oldPath = $"/blog/{oldSlug.ToLowerInvariant()}";
                var newPath = $"/blog/{cleanSlug.ToLowerInvariant()}";
                await _redirectService.AddRedirectAsync(oldPath, newPath, 301);
                await _auditLogService.LogAsync(CurrentAdminName, "URL Yönlendirme (301)", "UrlRedirect", $"Blog: {oldPath} -> {newPath}");
            }
        }

        existing.Title = model.Title;
        existing.Slug = cleanSlug;
        existing.Summary = model.Summary;
        existing.Content = model.Content;
        existing.Category = model.Category;
        existing.Author = !string.IsNullOrWhiteSpace(model.Author) ? model.Author : existing.Author;
        existing.MetaTitle = model.MetaTitle;
        existing.MetaDescription = model.MetaDescription;
        existing.IsPublished = model.IsPublished;

        await _blogService.UpdatePostAsync(existing, CurrentAdminName);
        TempData["AdminSuccess"] = $"'{existing.Title}' başlıklı blog yazısı başarıyla güncellendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Güncelleme", "BlogPost", $"Yazı: {existing.Title}");
        return RedirectToAction(nameof(Blog));
    }

    [HttpPost("DeletePost")]
    [HttpPost("DeletePost/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.BlogDelete)]
    public async Task<IActionResult> DeletePost(int id)
    {
        var post = await _blogService.GetPostByIdAsync(id);
        if (post != null)
        {
            await _blogService.SoftDeletePostAsync(id, CurrentAdminName);
            TempData["AdminSuccess"] = $"'{post.Title}' başlıklı blog yazısı çöp kutusuna taşındı.";
            await _auditLogService.LogAsync(CurrentAdminName, "Çöpe Taşıma", "BlogPost", $"Yazı: {post.Title}");
        }
        return RedirectToAction(nameof(Blog));
    }
}
