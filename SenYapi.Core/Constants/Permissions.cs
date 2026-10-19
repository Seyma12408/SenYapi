using System.Collections.Generic;

namespace SenYapi.Core.Constants;

public static class Permissions
{
    // Yönetim Paneli
    public const string AdminAccess = "Admin.Access";

    // Blog Yönetimi
    public const string BlogView = "Blog.View";
    public const string BlogCreate = "Blog.Create";
    public const string BlogEdit = "Blog.Edit";
    public const string BlogDelete = "Blog.Delete";
    public const string BlogPublish = "Blog.Publish";

    // Proje Yönetimi
    public const string ProjectView = "Project.View";
    public const string ProjectCreate = "Project.Create";
    public const string ProjectEdit = "Project.Edit";
    public const string ProjectDelete = "Project.Delete";

    // Sayfa & İçerik (Hizmetler ve Ekip)
    public const string ContentView = "Content.View";
    public const string ContentCreate = "Content.Create";
    public const string ContentEdit = "Content.Edit";
    public const string ContentDelete = "Content.Delete";

    // Görsel & Medya
    public const string MediaUpload = "Media.Upload";
    public const string MediaDelete = "Media.Delete";

    // Yorum Yönetimi
    public const string CommentView = "Comment.View";
    public const string CommentReply = "Comment.Reply";
    public const string CommentApprove = "Comment.Approve";
    public const string CommentDelete = "Comment.Delete";

    // İletişim Mesajları
    public const string MessageView = "Message.View";
    public const string MessageReply = "Message.Reply";
    public const string MessageDelete = "Message.Delete";

    // Site Ayarları
    public const string SettingsEdit = "Settings.Edit";

    public static readonly List<PermissionItem> All = new()
    {
        // Yönetim Paneli
        new PermissionItem(AdminAccess, "Panele Erişim", "Yönetim paneline giriş yapabilme ve paneli görüntüleme yetkisi", "Yönetim Paneli"),

        // Blog
        new PermissionItem(BlogView, "Blogları Görüntüleme", "Blog listesini ve detaylarını görüntüleme", "Blog Yönetimi", AdminAccess),
        new PermissionItem(BlogCreate, "Blog Ekleme", "Yeni blog yazısı oluşturma", "Blog Yönetimi", BlogView),
        new PermissionItem(BlogEdit, "Blog Düzenleme", "Mevcut blog yazılarını ve slug adresini düzenleme", "Blog Yönetimi", BlogView),
        new PermissionItem(BlogPublish, "Blog Yayımlama", "Blog yazılarını yayına alma veya taslağa çekme", "Blog Yönetimi", BlogView),
        new PermissionItem(BlogDelete, "Blog Silme", "Blog yazılarını silme / çöpe taşıma", "Blog Yönetimi", BlogView),

        // Proje
        new PermissionItem(ProjectView, "Projeleri Görüntüleme", "Projeler listesini ve detaylarını görüntüleme", "Proje Yönetimi", AdminAccess),
        new PermissionItem(ProjectCreate, "Proje Ekleme", "Yeni proje kaydı oluşturma", "Proje Yönetimi", ProjectView),
        new PermissionItem(ProjectEdit, "Proje Düzenleme", "Mevcut proje bilgilerini ve kapak görselini güncelleme", "Proje Yönetimi", ProjectView),
        new PermissionItem(ProjectDelete, "Proje Silme", "Projeleri silme / çöpe taşıma", "Proje Yönetimi", ProjectView),

        // Sayfa & İçerik (Hizmetler ve Ekip)
        new PermissionItem(ContentView, "İçerikleri Görüntüleme", "Hizmetler ve ekip üyeleri listesini görüntüleme", "Sayfa & İçerik Yönetimi", AdminAccess),
        new PermissionItem(ContentCreate, "İçerik Ekleme", "Yeni hizmet veya ekip üyesi ekleme", "Sayfa & İçerik Yönetimi", ContentView),
        new PermissionItem(ContentEdit, "İçerik Düzenleme", "Mevcut hizmet veya ekip üyesi bilgilerini güncelleme", "Sayfa & İçerik Yönetimi", ContentView),
        new PermissionItem(ContentDelete, "İçerik Silme", "Hizmet veya ekip üyesi kaydını silme", "Sayfa & İçerik Yönetimi", ContentView),

        // Görsel & Medya
        new PermissionItem(MediaUpload, "Görsel Yükleme", "Sunucuya yeni proje ve blog görselleri yükleme", "Görsel & Medya", AdminAccess),
        new PermissionItem(MediaDelete, "Görsel Silme", "Yüklenmiş görselleri sunucudan silme", "Görsel & Medya", MediaUpload),

        // Yorumlar
        new PermissionItem(CommentView, "Yorumları Görüntüleme", "Gelen kullanıcı yorumlarını ve yanıtları listeleme", "Yorum Yönetimi", AdminAccess),
        new PermissionItem(CommentReply, "Yorum Yanıtlama", "Kullanıcı yorumlarına yetkili olarak yanıt yazma", "Yorum Yönetimi", CommentView),
        new PermissionItem(CommentApprove, "Yorum Onaylama", "Bekleyen yorumları onaylama veya yayından kaldırma", "Yorum Yönetimi", CommentView),
        new PermissionItem(CommentDelete, "Yorum Silme", "Uygunsuz yorumları silme", "Yorum Yönetimi", CommentView),

        // İletişim Mesajları
        new PermissionItem(MessageView, "Mesajları Görüntüleme", "İletişim formundan gelen mesajları okuma", "İletişim Mesajları", AdminAccess),
        new PermissionItem(MessageReply, "Mesaj Yanıtlama", "Gelen mesajlara e-posta ile yanıt gönderme", "İletişim Mesajları", MessageView),
        new PermissionItem(MessageDelete, "Mesaj Silme", "İletişim mesajlarını silme", "İletişim Mesajları", MessageView),

        // Site Ayarları
        new PermissionItem(SettingsEdit, "Site Ayarlarını Düzenleme", "Firma adı, adres, telefon ve genel site ayarlarını güncelleme", "Site Ayarları", AdminAccess)
    };
}

public class PermissionItem
{
    public string Key { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string? ParentDependency { get; set; }

    public PermissionItem(string key, string name, string description, string category, string? parentDependency = null)
    {
        Key = key;
        Name = name;
        Description = description;
        Category = category;
        ParentDependency = parentDependency;
    }
}
