using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class SiteSetting : BaseEntity
{
    public string SiteTitle { get; set; } = "Şen Yapı İnşaat A.Ş.";
    public string Tagline { get; set; } = "Kaliteli ve Güvenilir İnşaat Hizmetleri";
    public string Phone { get; set; } = "+90 (212) 555 0123";
    public string Email { get; set; } = "info@senyapi.com";
    public string Address { get; set; } = "Levent Mah. Büyükdere Cad. No:123, İstanbul";
    public string WorkingHours { get; set; } = "Pzt - Cmt: 08:30 - 18:00";
    
    // Social Links
    public string? TwitterUrl { get; set; } = "https://twitter.com";
    public string? FacebookUrl { get; set; } = "https://facebook.com";
    public string? InstagramUrl { get; set; } = "https://instagram.com";
    public string? LinkedInUrl { get; set; } = "https://linkedin.com";
    public string? YouTubeUrl { get; set; } = "https://youtube.com";

    // Logo & Banner
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? HeroTitle { get; set; } = "Geleceğin Yapılarını Güvenle İnşa Ediyoruz";
    public string? HeroSubtitle { get; set; } = "30 yılı aşkın tecrübe, modern mühendislik ve kusursuz mimari işçilik.";

    // Security Settings
    public int MaxFailedAccessAttempts { get; set; } = 3;
    public int DefaultLockoutMinutes { get; set; } = 15;
}
