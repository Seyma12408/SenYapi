using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class BlogService : IBlogService
{
    private readonly SenYapiDbContext _context;
    private readonly IContentVersionService _versionService;
    private readonly INotificationService _notificationService;
    private readonly IFileService _fileService;

    public BlogService(SenYapiDbContext context, IContentVersionService versionService, INotificationService notificationService, IFileService fileService)
    {
        _context = context;
        _versionService = versionService;
        _notificationService = notificationService;
        _fileService = fileService;
    }

    public async Task<IEnumerable<BlogPost>> GetAllPostsAsync(bool onlyPublished = false)
    {
        var query = _context.BlogPosts
            .Include(b => b.Comments.Where(c => !c.IsDeleted))
            .Where(b => !b.IsDeleted)
            .AsQueryable();

        if (onlyPublished)
            query = query.Where(b => b.IsPublished);

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<BlogPost>> GetTrashPostsAsync()
    {
        return await _context.BlogPosts
            .Where(b => b.IsDeleted)
            .OrderByDescending(b => b.DeletedAt)
            .ToListAsync();
    }

    public async Task<BlogPost?> GetPostByIdAsync(int id, bool includeDeleted = false)
    {
        var query = _context.BlogPosts
            .Include(b => b.Comments.Where(c => !c.IsDeleted))
            .AsQueryable();

        if (!includeDeleted)
            query = query.Where(b => !b.IsDeleted);

        return await query.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<BlogPost?> GetPostBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var cleanSlug = slug.Trim().ToLowerInvariant();
        var normalizedSlug = SlugHelper.GenerateSlug(slug);
        return await _context.BlogPosts
            .Include(b => b.Comments)
            .Where(b => !b.IsDeleted)
            .FirstOrDefaultAsync(b => b.Slug.ToLower() == cleanSlug || b.Slug.ToLower() == normalizedSlug);
    }

    public async Task<bool> IsSlugInUseAsync(string slug, int excludePostId = 0)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;
        var cleanSlug = SlugHelper.GenerateSlug(slug);
        if (string.IsNullOrWhiteSpace(cleanSlug)) return false;

        return await _context.BlogPosts.AnyAsync(b =>
            !b.IsDeleted &&
            b.Id != excludePostId &&
            b.Slug.ToLower() == cleanSlug.ToLower());
    }

    public async Task<BlogPost> CreatePostAsync(BlogPost post, string adminUserName)
    {
        if (string.IsNullOrWhiteSpace(post.Slug))
        {
            post.Slug = SlugHelper.GenerateSlug(post.Title);
        }
        else
        {
            post.Slug = SlugHelper.GenerateSlug(post.Slug);
        }

        if (string.IsNullOrWhiteSpace(post.Slug))
        {
            post.Slug = "yazi";
        }

        post.CreatedAt = DateTime.UtcNow;
        post.IsDeleted = false;

        await _context.BlogPosts.AddAsync(post);
        await _context.SaveChangesAsync();

        // Save Initial Version
        await _versionService.SaveVersionAsync("BlogPost", post.Id, post.Title, post.Content, post.Summary, adminUserName);

        return post;
    }

    public async Task UpdatePostAsync(BlogPost post, string adminUserName)
    {
        var existing = await _context.BlogPosts.AsNoTracking().FirstOrDefaultAsync(b => b.Id == post.Id);
        if (existing != null)
        {
            await _versionService.SaveVersionAsync("BlogPost", existing.Id, existing.Title, existing.Content, existing.Summary, adminUserName);
        }

        if (string.IsNullOrWhiteSpace(post.Slug))
        {
            post.Slug = SlugHelper.GenerateSlug(post.Title);
        }
        else
        {
            post.Slug = SlugHelper.GenerateSlug(post.Slug);
        }

        if (string.IsNullOrWhiteSpace(post.Slug))
        {
            post.Slug = "yazi";
        }

        _context.BlogPosts.Update(post);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeletePostAsync(int id, string adminUserName)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post != null)
        {
            post.IsDeleted = true;
            post.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RestorePostAsync(int id, string adminUserName)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post != null)
        {
            post.IsDeleted = false;
            post.DeletedAt = null;
            if (await IsSlugInUseAsync(post.Slug, post.Id))
            {
                int count = 1;
                string baseSlug = post.Slug;
                while (await IsSlugInUseAsync($"{baseSlug}-{count}", post.Id))
                {
                    count++;
                }
                post.Slug = $"{baseSlug}-{count}";
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task PermanentDeletePostAsync(int id, string adminUserName)
    {
        var post = await _context.BlogPosts.Include(b => b.Comments).FirstOrDefaultAsync(b => b.Id == id);
        if (post != null)
        {
            _fileService.DeleteFile(post.ImageUrl);
            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();
        }
    }

    public async Task IncrementViewCountAsync(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post != null)
        {
            post.ViewCount++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddCommentAsync(BlogComment comment)
    {
        comment.CreatedAt = DateTime.UtcNow;
        if (!comment.IsStaffReply)
        {
            comment.IsApproved = false;
        }
        comment.IsDeleted = false;
        await _context.BlogComments.AddAsync(comment);
        await _context.SaveChangesAsync();

        if (comment.ParentCommentId.HasValue)
        {
            var parent = await _context.BlogComments.Include(c => c.BlogPost).FirstOrDefaultAsync(c => c.Id == comment.ParentCommentId.Value);
            if (parent != null && !string.IsNullOrEmpty(parent.UserId) && parent.UserId != comment.UserId)
            {
                await _notificationService.SendNotificationAsync(
                    parent.UserId,
                    "Yorumunuza Yeni Bir Yanıt Geldi",
                    $"{comment.Name} yorumunuza yanıt verdi.",
                    $"/blog/{parent.BlogPost?.Slug ?? ""}"
                );
            }
        }
    }

    public async Task<IEnumerable<BlogComment>> GetAllCommentsAsync()
    {
        return await _context.BlogComments
            .Include(c => c.BlogPost)
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BlogComment>> GetCommentsByUserIdAsync(string userId)
    {
        return await _context.BlogComments
            .Include(c => c.BlogPost)
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<BlogComment?> GetCommentByIdAsync(int id)
    {
        return await _context.BlogComments
            .Include(c => c.BlogPost)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task ApproveCommentAsync(int id)
    {
        var comment = await _context.BlogComments.Include(c => c.BlogPost).FirstOrDefaultAsync(c => c.Id == id);
        if (comment != null)
        {
            comment.IsApproved = true;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(comment.UserId))
            {
                await _notificationService.SendNotificationAsync(
                    comment.UserId,
                    "Yorumunuz Onaylandı",
                    $"'{comment.BlogPost?.Title}' makalesine yazdığınız yorum onaylanarak yayımlandı.",
                    $"/blog/{comment.BlogPost?.Slug}"
                );
            }
        }
    }

    public async Task RejectCommentAsync(int id)
    {
        var comment = await _context.BlogComments.FindAsync(id);
        if (comment != null)
        {
            comment.IsApproved = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteCommentAsync(int id)
    {
        var comment = await _context.BlogComments.FindAsync(id);
        if (comment != null)
        {
            comment.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteUserCommentAsync(int id, string userId)
    {
        var comment = await _context.BlogComments.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (comment != null)
        {
            comment.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ReplyCommentAsync(int id, string replyText)
    {
        var comment = await _context.BlogComments.Include(c => c.BlogPost).FirstOrDefaultAsync(c => c.Id == id);
        if (comment != null)
        {
            comment.AdminReply = replyText;
            comment.AdminReplyAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(comment.UserId))
            {
                await _notificationService.SendNotificationAsync(
                    comment.UserId,
                    "Yorumunuza Yanıt Verildi",
                    $"Şen Yapı yöneticisi '{comment.BlogPost?.Title}' makalesindeki yorumunuza yanıt verdi.",
                    $"/blog/{comment.BlogPost?.Slug}"
                );
            }
        }
    }

    public async Task UpdateCommentAsync(BlogComment comment)
    {
        comment.UpdatedAt = DateTime.UtcNow;
        comment.IsApproved = false;
        _context.BlogComments.Update(comment);
        await _context.SaveChangesAsync();
    }
}
