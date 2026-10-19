using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace SenYapi.Business.Services;

public interface IFileService
{
    Task<(bool Success, string? FilePath, string? ErrorMessage)> UploadImageAsync(IFormFile? file, string folderName = "uploads", string? preferredSlugOrName = null);
    Task<(bool Success, string? FilePath, string? ErrorMessage)> UploadPdfAsync(IFormFile? file, string folderName = "brochures");
    bool DeleteFile(string? relativePath);
}

public class FileService : IFileService
{
    private readonly IWebHostEnvironment _env;
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".svg", ".ico" };
    private const long MaxImageFileSize = 5 * 1024 * 1024; // 5 MB
    private const long MaxPdfFileSize = 15 * 1024 * 1024; // 15 MB

    public FileService(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string GetWebRootPath()
    {
        var webRoot = _env?.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            var contentRoot = _env?.ContentRootPath ?? Directory.GetCurrentDirectory();
            webRoot = Path.Combine(contentRoot, "wwwroot");
        }
        return webRoot;
    }

    public async Task<(bool Success, string? FilePath, string? ErrorMessage)> UploadImageAsync(IFormFile? file, string folderName = "uploads", string? preferredSlugOrName = null)
    {
        if (file == null || file.Length == 0)
            return (false, null, "Lütfen geçerli bir görsel dosyası seçiniz.");

        if (file.Length > MaxImageFileSize)
            return (false, null, "Görsel boyutu 5 MB'den büyük olamaz.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (Array.IndexOf(_allowedImageExtensions, ext) < 0)
            return (false, null, "Yalnızca JPG, JPEG, PNG, WEBP, SVG veya ICO formatında görseller yüklenebilir.");

        // MIME check
        if (!file.ContentType.StartsWith("image/"))
            return (false, null, "Yüklenen dosya geçerli bir görsel türü değil.");

        return await SaveFileInternalAsync(file, folderName, ext, preferredSlugOrName);
    }

    public async Task<(bool Success, string? FilePath, string? ErrorMessage)> UploadPdfAsync(IFormFile? file, string folderName = "brochures")
    {
        if (file == null || file.Length == 0)
            return (false, null, "Lütfen geçerli bir PDF dosyası seçiniz.");

        if (file.Length > MaxPdfFileSize)
            return (false, null, "PDF dosya boyutu 15 MB'den büyük olamaz.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return (false, null, "Yalnızca .PDF uzantılı broşür dosyaları yüklenebilir.");

        if (file.ContentType != "application/pdf")
            return (false, null, "Yüklenen dosya geçerli bir PDF formatında değil.");

        return await SaveFileInternalAsync(file, folderName, ext);
    }

    private async Task<(bool Success, string? FilePath, string? ErrorMessage)> SaveFileInternalAsync(IFormFile file, string folderName, string ext, string? preferredSlugOrName = null)
    {
        try
        {
            var webRootPath = GetWebRootPath();
            var uploadDir = Path.Combine(webRootPath, "uploads", folderName);
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            // Orijinal dosya adını al veya belirtilen ön eki kullan:
            // "Kampüs Dış Cephe.jpg" -> "Kampüs Dış Cephe"
            string rawPrefix = !string.IsNullOrWhiteSpace(preferredSlugOrName)
                ? preferredSlugOrName
                : Path.GetFileNameWithoutExtension(file.FileName);

            // Türkçe karakterleri dönüştür, boşlukları tire yap, güvenli olmayan karakterleri temizle
            var cleanPrefix = SlugHelper.GenerateSlug(rawPrefix);
            if (string.IsNullOrWhiteSpace(cleanPrefix))
            {
                cleanPrefix = folderName.Replace("/", "-");
            }

            // Okunabilir ve benzersiz sunucu dosya adı: [temiz-ad]-[benzersiz-ek].[uzanti]
            // Örnek: "kampus-dis-cephe-8a7b3c2d.jpg"
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"{cleanPrefix}-{uniqueSuffix}{ext}";

            // Aynı adlı dosyaların birbirinin üzerine yazılmasını kesin olarak engelle
            var fullPath = Path.Combine(uploadDir, fileName);
            while (File.Exists(fullPath))
            {
                uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
                fileName = $"{cleanPrefix}-{uniqueSuffix}{ext}";
                fullPath = Path.Combine(uploadDir, fileName);
            }

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Web adresi formatında /uploads/... olarak döner
            var relativePath = $"/uploads/{folderName}/{fileName}";
            return (true, relativePath, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Dosya kaydedilirken hata oluştu: {ex.Message}");
        }
    }

    public bool DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;

        // Do NOT delete original builderz theme assets
        if (relativePath.Contains("/theme/builderz/", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var cleanPath = relativePath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
            var webRoot = GetWebRootPath();
            var fullPath = Path.Combine(webRoot, cleanPath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
        }
        catch
        {
            // non-fatal
        }
        return false;
    }
}
