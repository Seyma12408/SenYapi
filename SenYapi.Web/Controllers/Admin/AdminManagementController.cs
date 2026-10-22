using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Core.Constants;
using SenYapi.Entity;
using SenYapi.Web.Filters;
using SenYapi.Web.Models;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
public class AdminManagementController : AdminBaseController
{
    private readonly IServiceItemService _serviceService;
    private readonly ITeamService _teamService;
    private readonly ISiteSettingService _settingService;
    private readonly IProjectService _projectService;
    private readonly IBlogService _blogService;
    private readonly IContentVersionService _versionService;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;

    public AdminManagementController(
        IServiceItemService serviceService,
        ITeamService teamService,
        ISiteSettingService settingService,
        IProjectService projectService,
        IBlogService blogService,
        IContentVersionService versionService,
        IFileService fileService,
        IAuditLogService auditLogService)
    {
        _serviceService = serviceService;
        _teamService = teamService;
        _settingService = settingService;
        _projectService = projectService;
        _blogService = blogService;
        _versionService = versionService;
        _fileService = fileService;
        _auditLogService = auditLogService;
    }

    // ==========================================
    // 1. SERVICES
    // ==========================================
    [HttpGet("Services")]
    [HasPermission(Permissions.ContentView)]
    public async Task<IActionResult> Services()
    {
        var services = await _serviceService.GetAllServicesAsync();
        return View("~/Views/Admin/Services.cshtml", services.ToList());
    }

    [HttpPost("AddService")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ContentCreate)]
    public async Task<IActionResult> AddService(ServiceItem model, IFormFile? imageFile)
    {
        if (!ValidationHelper.IsCleanText(model.Title, 2, 150))
        {
            TempData["AdminError"] = "Hizmet başlığı en az 2, en fazla 150 karakter olmalıdır.";
            return RedirectToAction(nameof(Services));
        }

        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "services");
            if (success)
            {
                model.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Services));
            }
        }
        else if (string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            model.ImageUrl = "~/theme/builderz/img/service-1.jpg";
        }

        await _serviceService.AddServiceAsync(model);
        TempData["AdminSuccess"] = $"'{model.Title}' hizmeti başarıyla eklendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Ekleme", "ServiceItem", $"Hizmet: {model.Title}");
        return RedirectToAction(nameof(Services));
    }

    [HttpPost("UpdateService")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ContentEdit)]
    public async Task<IActionResult> UpdateService(ServiceItem model, IFormFile? imageFile)
    {
        if (!ValidationHelper.IsCleanText(model.Title, 2, 150))
        {
            TempData["AdminError"] = "Hizmet başlığı en az 2, en fazla 150 karakter olmalıdır.";
            return RedirectToAction(nameof(Services));
        }

        var existing = await _serviceService.GetServiceByIdAsync(model.Id);
        if (existing == null)
        {
            TempData["AdminError"] = "Güncellenecek hizmet bulunamadı.";
            return RedirectToAction(nameof(Services));
        }

        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "services");
            if (success)
            {
                existing.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Services));
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            existing.ImageUrl = model.ImageUrl;
        }

        existing.Title = model.Title;
        existing.Description = model.Description;
        existing.IsActive = model.IsActive;

        await _serviceService.UpdateServiceAsync(existing);
        TempData["AdminSuccess"] = $"'{existing.Title}' hizmeti başarıyla güncellendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Güncelleme", "ServiceItem", $"Hizmet: {existing.Title}");
        return RedirectToAction(nameof(Services));
    }

    [HttpPost("DeleteService")]
    [HttpPost("DeleteService/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ContentDelete)]
    public async Task<IActionResult> DeleteService(int id)
    {
        await _serviceService.DeleteServiceAsync(id);
        TempData["AdminSuccess"] = "Hizmet silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Silme", "ServiceItem", $"ServiceId: {id}");
        return RedirectToAction(nameof(Services));
    }

    // ==========================================
    // 2. TEAM
    // ==========================================
    [HttpGet("Team")]
    [HasPermission(Permissions.ContentView)]
    public async Task<IActionResult> Team()
    {
        var team = await _teamService.GetAllTeamMembersAsync();
        return View("~/Views/Admin/Team.cshtml", team.ToList());
    }

    [HttpPost("AddTeamMember")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ContentCreate)]
    public async Task<IActionResult> AddTeamMember(TeamMember model, IFormFile? imageFile)
    {
        if (!ValidationHelper.IsCleanText(model.FullName, 2, 100) || !ValidationHelper.IsCleanText(model.Title, 2, 100))
        {
            TempData["AdminError"] = "Ad Soyad ve Ünvan alanları en az 2 karakter olmalıdır.";
            return RedirectToAction(nameof(Team));
        }

        if (!string.IsNullOrWhiteSpace(model.TwitterUrl) && !ValidationHelper.IsValidUrl(model.TwitterUrl))
        {
            TempData["AdminError"] = "Twitter bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }
        if (!string.IsNullOrWhiteSpace(model.FacebookUrl) && !ValidationHelper.IsValidUrl(model.FacebookUrl))
        {
            TempData["AdminError"] = "Facebook bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }
        if (!string.IsNullOrWhiteSpace(model.LinkedInUrl) && !ValidationHelper.IsValidUrl(model.LinkedInUrl))
        {
            TempData["AdminError"] = "LinkedIn bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }
        if (!string.IsNullOrWhiteSpace(model.InstagramUrl) && !ValidationHelper.IsValidUrl(model.InstagramUrl))
        {
            TempData["AdminError"] = "Instagram bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }

        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "team");
            if (success)
            {
                model.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Team));
            }
        }
        else if (string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            model.ImageUrl = "~/theme/builderz/img/team-1.jpg";
        }

        await _teamService.AddTeamMemberAsync(model);
        TempData["AdminSuccess"] = $"'{model.FullName}' ekip üyesi başarıyla eklendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Ekleme", "TeamMember", $"Ekip: {model.FullName}");
        return RedirectToAction(nameof(Team));
    }

    [HttpPost("UpdateTeamMember")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ContentEdit)]
    public async Task<IActionResult> UpdateTeamMember(TeamMember model, IFormFile? imageFile)
    {
        if (!ValidationHelper.IsCleanText(model.FullName, 2, 100) || !ValidationHelper.IsCleanText(model.Title, 2, 100))
        {
            TempData["AdminError"] = "Ad Soyad ve Ünvan alanları en az 2 karakter olmalıdır.";
            return RedirectToAction(nameof(Team));
        }

        if (!string.IsNullOrWhiteSpace(model.TwitterUrl) && !ValidationHelper.IsValidUrl(model.TwitterUrl))
        {
            TempData["AdminError"] = "Twitter bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }
        if (!string.IsNullOrWhiteSpace(model.FacebookUrl) && !ValidationHelper.IsValidUrl(model.FacebookUrl))
        {
            TempData["AdminError"] = "Facebook bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }
        if (!string.IsNullOrWhiteSpace(model.LinkedInUrl) && !ValidationHelper.IsValidUrl(model.LinkedInUrl))
        {
            TempData["AdminError"] = "LinkedIn bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }
        if (!string.IsNullOrWhiteSpace(model.InstagramUrl) && !ValidationHelper.IsValidUrl(model.InstagramUrl))
        {
            TempData["AdminError"] = "Instagram bağlantısı geçerli ve güvenli bir URL olmalıdır.";
            return RedirectToAction(nameof(Team));
        }

        var existing = await _teamService.GetTeamMemberByIdAsync(model.Id);
        if (existing == null)
        {
            TempData["AdminError"] = "Güncellenecek ekip üyesi bulunamadı.";
            return RedirectToAction(nameof(Team));
        }

        if (imageFile != null && imageFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(imageFile, "team");
            if (success)
            {
                existing.ImageUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error;
                return RedirectToAction(nameof(Team));
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            existing.ImageUrl = model.ImageUrl;
        }

        existing.FullName = model.FullName;
        existing.Title = model.Title;
        existing.TwitterUrl = model.TwitterUrl;
        existing.FacebookUrl = model.FacebookUrl;
        existing.LinkedInUrl = model.LinkedInUrl;
        existing.InstagramUrl = model.InstagramUrl;
        existing.DisplayOrder = model.DisplayOrder;
        existing.IsActive = model.IsActive;

        await _teamService.UpdateTeamMemberAsync(existing);
        TempData["AdminSuccess"] = $"'{existing.FullName}' ekip üyesi başarıyla güncellendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Güncelleme", "TeamMember", $"Ekip: {existing.FullName}");
        return RedirectToAction(nameof(Team));
    }

    [HttpPost("DeleteTeamMember")]
    [HttpPost("DeleteTeamMember/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.ContentDelete)]
    public async Task<IActionResult> DeleteTeamMember(int id)
    {
        await _teamService.DeleteTeamMemberAsync(id);
        TempData["AdminSuccess"] = "Ekip üyesi silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Silme", "TeamMember", $"TeamMemberId: {id}");
        return RedirectToAction(nameof(Team));
    }

    // ==========================================
    // 3. SETTINGS
    // ==========================================
    [HttpGet("Settings")]
    [HasPermission(Permissions.SettingsEdit)]
    public async Task<IActionResult> Settings()
    {
        var settings = await _settingService.GetSettingsAsync();
        var logs = await _auditLogService.GetRecentLogsAsync(50);
        ViewBag.AuditLogs = logs;
        return View("~/Views/Admin/Settings.cshtml", settings);
    }

    [HttpPost("UpdateSettings")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.SettingsEdit)]
    public async Task<IActionResult> UpdateSettings(SiteSetting model, IFormFile? logoFile, IFormFile? faviconFile)
    {
        if (string.IsNullOrWhiteSpace(model.SiteTitle) || !ValidationHelper.IsCleanText(model.SiteTitle, 2, 100))
        {
            TempData["AdminError"] = "Site başlığı en az 2, en fazla 100 karakter olmalıdır.";
            return RedirectToAction(nameof(Settings));
        }

        if (!string.IsNullOrWhiteSpace(model.Phone))
        {
            if (!PhoneHelper.IsValid(model.Phone, allowLandline: true))
            {
                TempData["AdminError"] = "Lütfen geçerli bir kurumsal telefon veya mobil numara giriniz (Örn: 0212 123 45 67 veya 0532 123 45 67).";
                return RedirectToAction(nameof(Settings));
            }
            model.Phone = PhoneHelper.FormatDisplay(model.Phone);
        }

        if (!string.IsNullOrWhiteSpace(model.Email) && !ValidationHelper.IsValidEmail(model.Email))
        {
            TempData["AdminError"] = "Lütfen geçerli bir kurumsal e-posta adresi giriniz.";
            return RedirectToAction(nameof(Settings));
        }

        if (!string.IsNullOrWhiteSpace(model.TwitterUrl) && !ValidationHelper.IsValidUrl(model.TwitterUrl))
        {
            TempData["AdminError"] = "Twitter/X URL adresi geçersiz.";
            return RedirectToAction(nameof(Settings));
        }

        if (!string.IsNullOrWhiteSpace(model.FacebookUrl) && !ValidationHelper.IsValidUrl(model.FacebookUrl))
        {
            TempData["AdminError"] = "Facebook URL adresi geçersiz.";
            return RedirectToAction(nameof(Settings));
        }

        if (!string.IsNullOrWhiteSpace(model.InstagramUrl) && !ValidationHelper.IsValidUrl(model.InstagramUrl))
        {
            TempData["AdminError"] = "Instagram URL adresi geçersiz.";
            return RedirectToAction(nameof(Settings));
        }

        if (!string.IsNullOrWhiteSpace(model.LinkedInUrl) && !ValidationHelper.IsValidUrl(model.LinkedInUrl))
        {
            TempData["AdminError"] = "LinkedIn URL adresi geçersiz.";
            return RedirectToAction(nameof(Settings));
        }

        if (!string.IsNullOrWhiteSpace(model.YouTubeUrl) && !ValidationHelper.IsValidUrl(model.YouTubeUrl))
        {
            TempData["AdminError"] = "YouTube URL adresi geçersiz.";
            return RedirectToAction(nameof(Settings));
        }

        if (logoFile != null && logoFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(logoFile, "settings");
            if (success)
            {
                model.LogoUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error ?? "Logo yüklenemedi.";
                return RedirectToAction(nameof(Settings));
            }
        }

        if (faviconFile != null && faviconFile.Length > 0)
        {
            var (success, filePath, error) = await _fileService.UploadImageAsync(faviconFile, "settings");
            if (success)
            {
                model.FaviconUrl = filePath!;
            }
            else
            {
                TempData["AdminError"] = error ?? "Favicon yüklenemedi.";
                return RedirectToAction(nameof(Settings));
            }
        }

        await _settingService.UpdateSettingsAsync(model, CurrentAdminName);
        TempData["AdminSuccess"] = "Site ayarları başarıyla güncellendi.";
        return RedirectToAction(nameof(Settings));
    }

    // ==========================================
    // 4. TRASH BIN
    // ==========================================
    [HttpGet("Trash")]
    public async Task<IActionResult> Trash()
    {
        var deletedProjects = await _projectService.GetTrashProjectsAsync();
        var deletedPosts = await _blogService.GetTrashPostsAsync();

        var model = new AdminTrashViewModel
        {
            Projects = deletedProjects.ToList(),
            BlogPosts = deletedPosts.ToList()
        };

        return View("~/Views/Admin/Trash.cshtml", model);
    }

    [HttpPost("RestoreProject")]
    [HttpPost("RestoreProject/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreProject(int id)
    {
        await _projectService.RestoreProjectAsync(id, CurrentAdminName);
        TempData["AdminSuccess"] = "Proje başarıyla geri yüklendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Geri Yükleme", "Project", $"ProjectId: {id}");
        return RedirectToAction(nameof(Trash));
    }

    [HttpPost("PermanentDeleteProject")]
    [HttpPost("PermanentDeleteProject/{id}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin,Admin")]
    public async Task<IActionResult> PermanentDeleteProject(int id)
    {
        await _projectService.PermanentDeleteProjectAsync(id, CurrentAdminName);
        TempData["AdminSuccess"] = "Proje ve tüm ilişkili verileri kalıcı olarak silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Kalıcı Silme", "Project", $"ProjectId: {id}");
        return RedirectToAction(nameof(Trash));
    }

    [HttpPost("RestorePost")]
    [HttpPost("RestorePost/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePost(int id)
    {
        await _blogService.RestorePostAsync(id, CurrentAdminName);
        TempData["AdminSuccess"] = "Blog yazısı başarıyla geri yüklendi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Geri Yükleme", "BlogPost", $"PostId: {id}");
        return RedirectToAction(nameof(Trash));
    }

    [HttpPost("PermanentDeletePost")]
    [HttpPost("PermanentDeletePost/{id}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin,Admin")]
    public async Task<IActionResult> PermanentDeletePost(int id)
    {
        await _blogService.PermanentDeletePostAsync(id, CurrentAdminName);
        TempData["AdminSuccess"] = "Blog yazısı ve tüm yorumları kalıcı olarak silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Kalıcı Silme", "BlogPost", $"PostId: {id}");
        return RedirectToAction(nameof(Trash));
    }

    // ==========================================
    // 5. CONTENT VERSIONS
    // ==========================================
    [HttpGet("Versions")]
    public async Task<IActionResult> Versions(string entityType, int entityId)
    {
        var versions = await _versionService.GetVersionsAsync(entityType, entityId);
        ViewBag.EntityType = entityType;
        ViewBag.EntityId = entityId;
        return View("~/Views/Admin/Versions.cshtml", versions.ToList());
    }

    [HttpPost("RollbackVersion")]
    [HttpPost("RollbackVersion/{versionId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollbackVersion(int versionId)
    {
        var version = await _versionService.GetVersionByIdAsync(versionId);
        if (version == null)
        {
            TempData["AdminError"] = "Sürüm kaydı bulunamadı.";
            return RedirectToAction("Index", "Admin");
        }

        if (version.EntityType == "Project")
        {
            var project = await _projectService.GetProjectByIdAsync(version.EntityId);
            if (project != null)
            {
                project.Title = version.Title;
                project.Description = version.Content;
                if (!string.IsNullOrEmpty(version.Summary))
                    project.CategoryName = version.Summary;

                await _projectService.UpdateProjectAsync(project, CurrentAdminName);
                TempData["AdminSuccess"] = $"Proje Sürüm {version.VersionNumber} durumuna başarıyla döndürüldü.";
                await _auditLogService.LogAsync(CurrentAdminName, "Sürüm Geri Yükleme", "Project", $"Proje: {project.Title}, Sürüm: {version.VersionNumber}");
                return RedirectToAction("Projects", "AdminProjects");
            }
        }
        else if (version.EntityType == "BlogPost")
        {
            var post = await _blogService.GetPostByIdAsync(version.EntityId);
            if (post != null)
            {
                post.Title = version.Title;
                post.Content = version.Content;
                post.Summary = version.Summary ?? string.Empty;

                await _blogService.UpdatePostAsync(post, CurrentAdminName);
                TempData["AdminSuccess"] = $"Blog Yazısı Sürüm {version.VersionNumber} durumuna başarıyla döndürüldü.";
                await _auditLogService.LogAsync(CurrentAdminName, "Sürüm Geri Yükleme", "BlogPost", $"Yazı: {post.Title}, Sürüm: {version.VersionNumber}");
                return RedirectToAction("Blog", "AdminBlog");
            }
        }

        TempData["AdminError"] = "Sürüm geri yüklenirken hedef varlık bulunamadı.";
        return RedirectToAction("Index", "AdminDashboard");
    }
}
