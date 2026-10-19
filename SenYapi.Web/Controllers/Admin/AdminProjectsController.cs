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
public class AdminProjectsController : AdminBaseController
{
    private readonly IProjectService _projectService;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUrlRedirectService _redirectService;

    public AdminProjectsController(
        IProjectService projectService,
        IFileService fileService,
        IAuditLogService auditLogService,
        IUrlRedirectService redirectService)
    {
        _projectService = projectService;
        _fileService = fileService;
        _auditLogService = auditLogService;
        _redirectService = redirectService;
    }

    [HttpGet("Projects")]
    [HasPermission(Permissions.ProjectView)]
    public async Task<IActionResult> Projects(string? search, string? category)
    {
        var projects = await _projectService.GetAllProjectsAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim().ToLowerInvariant();
            projects = projects.Where(p =>
                p.Title.ToLower().Contains(cleanSearch) ||
                (p.Description != null && p.Description.ToLower().Contains(cleanSearch)) ||
                (p.Location != null && p.Location.ToLower().Contains(cleanSearch)));
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
        {
            projects = projects.Where(p => p.Category == category);
        }

        ViewBag.Search = search;
        ViewBag.Category = category;

        return View("~/Views/Admin/Projects.cshtml", projects.ToList());
    }

    [HttpPost("AddProject")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ProjectCreate)]
    public async Task<IActionResult> AddProject(Project model, IFormFile? imageFile, IFormFile[]? galleryFiles, IFormFile? pdfFile)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            TempData["AdminError"] = "Proje adı zorunludur.";
            return RedirectToAction(nameof(Projects));
        }

        // Slug handling & validation
        string finalSlug;
        if (string.IsNullOrWhiteSpace(model.Slug))
        {
            var baseSlug = SlugHelper.GenerateSlug(model.Title);
            if (string.IsNullOrWhiteSpace(baseSlug))
            {
                TempData["AdminError"] = "Proje başlığından geçerli bir kalıcı bağlantı (slug) üretilemedi.";
                return RedirectToAction(nameof(Projects));
            }
            finalSlug = await _projectService.SuggestUniqueSlugAsync(baseSlug);
        }
        else
        {
            var cleanSlug = SlugHelper.GenerateSlug(model.Slug);
            if (string.IsNullOrWhiteSpace(cleanSlug))
            {
                TempData["AdminError"] = "Girilen kalıcı bağlantı (slug) geçersiz karakterler içeriyor.";
                return RedirectToAction(nameof(Projects));
            }

            if (await _projectService.IsSlugInUseAsync(cleanSlug))
            {
                var suggestion = await _projectService.SuggestUniqueSlugAsync(cleanSlug);
                TempData["AdminError"] = $"'{cleanSlug}' kalıcı bağlantısı başka bir projede kullanılıyor. Önerilen benzersiz bağlantı: '{suggestion}'";
                return RedirectToAction(nameof(Projects));
            }
            finalSlug = cleanSlug;
        }
        model.Slug = finalSlug;

        // Cover Image upload
        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "projects");
            if (success)
            {
                model.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Projects));
            }
        }
        else if (string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            model.ImageUrl = "~/theme/builderz/img/portfolio-1.jpg";
        }

        // PDF Brochure upload
        if (pdfFile != null && pdfFile.Length > 0)
        {
            var (pdfSuccess, pdfPath, pdfError) = await _fileService.UploadPdfAsync(pdfFile, "brochures");
            if (pdfSuccess)
            {
                model.PdfBrochureUrl = pdfPath!;
            }
            else
            {
                TempData["AdminError"] = pdfError;
                return RedirectToAction(nameof(Projects));
            }
        }

        model.CategoryName = model.Category switch
        {
            "complete" => "Tamamlanan",
            "running" => "Devam Eden",
            "upcoming" => "Gelecek Proje",
            _ => "Tamamlanan"
        };
        model.PricePrefix = !string.IsNullOrWhiteSpace(model.PricePrefix) ? model.PricePrefix : "Başlangıç";

        await _projectService.AddProjectAsync(model, CurrentAdminName);

        // Upload Gallery Images if any
        if (galleryFiles != null && galleryFiles.Length > 0)
        {
            int order = 1;
            foreach (var file in galleryFiles)
            {
                var (success, filePath, _) = await _fileService.UploadImageAsync(file, "projects/gallery");
                if (success)
                {
                    await _projectService.AddProjectImageAsync(new ProjectImage
                    {
                        ProjectId = model.Id,
                        ImageUrl = filePath!,
                        AltText = model.Title,
                        DisplayOrder = order++
                    });
                }
            }
        }

        TempData["AdminSuccess"] = $"'{model.Title}' projesi başarıyla eklendi. (Adres: /projeler/{model.Slug})";
        await _auditLogService.LogAsync(CurrentAdminName, "Ekleme", "Project", $"Proje: {model.Title}");
        return RedirectToAction(nameof(Projects));
    }

    [HttpPost("UpdateProject")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ProjectEdit)]
    public async Task<IActionResult> UpdateProject(Project model, IFormFile? imageFile, IFormFile[]? galleryFiles, IFormFile? pdfFile)
    {
        var existing = await _projectService.GetProjectByIdAsync(model.Id);
        if (existing == null)
        {
            TempData["AdminError"] = "Güncellenecek proje bulunamadı.";
            return RedirectToAction(nameof(Projects));
        }

        var oldSlug = existing.Slug;
        string finalSlug = existing.Slug;

        // Slug validation & 301 Redirect
        if (!string.IsNullOrWhiteSpace(model.Slug))
        {
            var cleanSlug = SlugHelper.GenerateSlug(model.Slug);
            if (string.IsNullOrWhiteSpace(cleanSlug))
            {
                TempData["AdminError"] = "Girilen kalıcı bağlantı (slug) geçersiz karakterler içeriyor.";
                return RedirectToAction(nameof(Projects));
            }

            if (await _projectService.IsSlugInUseAsync(cleanSlug, existing.Id))
            {
                var suggestion = await _projectService.SuggestUniqueSlugAsync(cleanSlug, existing.Id);
                TempData["AdminError"] = $"'{cleanSlug}' kalıcı bağlantısı başka bir projede kullanılıyor. Önerilen benzersiz bağlantı: '{suggestion}'";
                return RedirectToAction(nameof(Projects));
            }
            finalSlug = cleanSlug;
        }

        // Automatic 301 Redirect if slug changed
        if (!string.Equals(oldSlug, finalSlug, StringComparison.OrdinalIgnoreCase))
        {
            var oldPath = $"/projeler/{oldSlug}";
            var newPath = $"/projeler/{finalSlug}";
            await _redirectService.AddRedirectAsync(oldPath, newPath, 301);
            await _auditLogService.LogAsync(CurrentAdminName, "URL Yönlendirme (301)", "UrlRedirect", $"Eski: {oldPath} -> Yeni: {newPath}");
        }

        // Image upload: update if new file provided, keep existing otherwise
        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "projects");
            if (success)
            {
                existing.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Projects));
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            existing.ImageUrl = model.ImageUrl;
        }

        // PDF Brochure upload
        if (pdfFile != null && pdfFile.Length > 0)
        {
            var (pdfSuccess, pdfPath, pdfError) = await _fileService.UploadPdfAsync(pdfFile, "brochures");
            if (pdfSuccess)
            {
                existing.PdfBrochureUrl = pdfPath!;
            }
            else
            {
                TempData["AdminError"] = pdfError;
                return RedirectToAction(nameof(Projects));
            }
        }

        existing.Title = model.Title;
        existing.Slug = finalSlug;
        existing.Description = model.Description;
        existing.Category = model.Category;
        existing.CategoryName = model.Category switch
        {
            "complete" => "Tamamlanan",
            "running" => "Devam Eden",
            "upcoming" => "Gelecek Proje",
            _ => "Tamamlanan"
        };
        existing.Location = model.Location;
        existing.Address = model.Address;
        existing.MapEmbedUrl = model.MapEmbedUrl;
        existing.EstimatedDeliveryDate = model.EstimatedDeliveryDate;
        existing.ApartmentTypes = model.ApartmentTypes;
        existing.AreaSquareMeters = model.AreaSquareMeters;
        existing.ShowPrice = model.ShowPrice;
        existing.Price = model.Price;
        existing.PricePrefix = !string.IsNullOrWhiteSpace(model.PricePrefix) ? model.PricePrefix : "Başlangıç";
        existing.Currency = !string.IsNullOrWhiteSpace(model.Currency) ? model.Currency : "TL";
        existing.DisplayOrder = model.DisplayOrder;
        existing.IsActive = model.IsActive;

        await _projectService.UpdateProjectAsync(existing, CurrentAdminName);

        // Upload Gallery Images if any
        if (galleryFiles != null && galleryFiles.Length > 0)
        {
            int order = (existing.Images?.Count ?? 0) + 1;
            foreach (var file in galleryFiles)
            {
                var (success, filePath, _) = await _fileService.UploadImageAsync(file, "projects/gallery");
                if (success)
                {
                    await _projectService.AddProjectImageAsync(new ProjectImage
                    {
                        ProjectId = existing.Id,
                        ImageUrl = filePath!,
                        AltText = existing.Title,
                        DisplayOrder = order++
                    });
                }
            }
        }

        TempData["AdminSuccess"] = $"'{existing.Title}' projesi başarıyla güncellendi. (Adres: /projeler/{existing.Slug})";
        await _auditLogService.LogAsync(CurrentAdminName, "Güncelleme", "Project", $"Proje: {existing.Title}");
        return RedirectToAction(nameof(Projects));
    }

    [HttpPost("DeleteProject")]
    [HttpPost("DeleteProject/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ProjectDelete)]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        if (project != null)
        {
            await _projectService.SoftDeleteProjectAsync(id, CurrentAdminName);
            TempData["AdminSuccess"] = $"'{project.Title}' projesi çöp kutusuna taşındı.";
            await _auditLogService.LogAsync(CurrentAdminName, "Çöpe Taşıma", "Project", $"Proje: {project.Title}");
        }
        return RedirectToAction(nameof(Projects));
    }

    [HttpPost("DeleteProjectImage")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MediaDelete)]
    public async Task<IActionResult> DeleteProjectImage(int imageId, int projectId)
    {
        await _projectService.DeleteProjectImageAsync(imageId);
        TempData["AdminSuccess"] = "Galeri görseli başarıyla silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Galeri Görseli Silme", "ProjectImage", $"ImageId: {imageId}, ProjectId: {projectId}");
        return RedirectToAction(nameof(Projects));
    }
}
