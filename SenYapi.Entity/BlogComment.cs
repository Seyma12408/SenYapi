using System;
using System.Collections.Generic;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class BlogComment : BaseEntity
{
    public int BlogPostId { get; set; }
    public BlogPost? BlogPost { get; set; }
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;
    
    // Geriye dönük uyumluluk için eski alanlar korunur
    public string? AdminReply { get; set; }
    public DateTime? AdminReplyAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Çok Katmanlı / Karşılıklı Devam Eden Yanıtlama (Threaded Replies)
    public int? ParentCommentId { get; set; }
    public BlogComment? ParentComment { get; set; }
    public List<BlogComment> Replies { get; set; } = new();

    // Rol ve yetkili etiketi (Örn: "Sistem Yöneticisi", "Editör", "Üye")
    public string? UserRole { get; set; }
    public bool IsStaffReply { get; set; } = false;
}
