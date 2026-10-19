using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SenYapi.Entity;

namespace SenYapi.DataAccess;

public class SenYapiDbContext : IdentityDbContext<AppUser, AppRole, string>
{
    public SenYapiDbContext(DbContextOptions<SenYapiDbContext> options) : base(options)
    {
    }

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<BlogComment> BlogComments => Set<BlogComment>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UrlRedirect> UrlRedirects => Set<UrlRedirect>();

    // New Portal & Extended Features DbSets
    public DbSet<FavoriteProject> FavoriteProjects => Set<FavoriteProject>();
    public DbSet<ProjectInquiry> ProjectInquiries => Set<ProjectInquiry>();
    public DbSet<ProjectAppointment> ProjectAppointments => Set<ProjectAppointment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<ContactMessageReply> ContactMessageReplies => Set<ContactMessageReply>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ContactMessage>()
            .HasMany(m => m.Replies)
            .WithOne(r => r.ContactMessage)
            .HasForeignKey(r => r.ContactMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BlogPost>()
            .HasMany(b => b.Comments)
            .WithOne(c => c.BlogPost)
            .HasForeignKey(c => c.BlogPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BlogComment>()
            .HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Project>()
            .HasMany(p => p.Images)
            .WithOne(i => i.Project)
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Project>()
            .HasIndex(p => p.Slug)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Entity<FavoriteProject>()
            .HasIndex(f => new { f.UserId, f.ProjectId })
            .IsUnique();

        builder.Entity<Conversation>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
