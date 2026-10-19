using System;
using System.Collections.Generic;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class Project : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "complete", "running", "upcoming"
    public string CategoryName { get; set; } = string.Empty; // "Completed", "Running", "Upcoming"
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Detailed Specifications
    public string? Location { get; set; }
    public string? Address { get; set; }
    public string? MapEmbedUrl { get; set; }
    public string? EstimatedDeliveryDate { get; set; }
    public string? ApartmentTypes { get; set; }
    public string? AreaSquareMeters { get; set; }
    public string? PdfBrochureUrl { get; set; }

    // Price Configuration
    public bool ShowPrice { get; set; } = false;
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "TL";
    public string PricePrefix { get; set; } = "Başlangıç Fiyatı:";

    // Trash / Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public List<ProjectImage> Images { get; set; } = new();
    public List<FavoriteProject> Favorites { get; set; } = new();
    public List<ProjectInquiry> Inquiries { get; set; } = new();
    public List<ProjectAppointment> Appointments { get; set; } = new();
}
