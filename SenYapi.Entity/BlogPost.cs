using System;
using System.Collections.Generic;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class BlogPost : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    public int ViewCount { get; set; } = 0;
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Trash / Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public List<BlogComment> Comments { get; set; } = new();
}
