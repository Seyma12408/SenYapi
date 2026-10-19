using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IBlogService
{
    Task<IEnumerable<BlogPost>> GetAllPostsAsync(bool onlyPublished = false);
    Task<IEnumerable<BlogPost>> GetTrashPostsAsync();
    Task<BlogPost?> GetPostByIdAsync(int id, bool includeDeleted = false);
    Task<BlogPost?> GetPostBySlugAsync(string slug);
    Task<bool> IsSlugInUseAsync(string slug, int excludePostId = 0);
    Task<BlogPost> CreatePostAsync(BlogPost post, string adminUserName);
    Task UpdatePostAsync(BlogPost post, string adminUserName);
    Task SoftDeletePostAsync(int id, string adminUserName);
    Task RestorePostAsync(int id, string adminUserName);
    Task PermanentDeletePostAsync(int id, string adminUserName);
    Task IncrementViewCountAsync(int id);

    // Comments
    Task AddCommentAsync(BlogComment comment);
    Task<IEnumerable<BlogComment>> GetAllCommentsAsync();
    Task<IEnumerable<BlogComment>> GetCommentsByUserIdAsync(string userId);
    Task<BlogComment?> GetCommentByIdAsync(int id);
    Task ApproveCommentAsync(int id);
    Task RejectCommentAsync(int id);
    Task DeleteCommentAsync(int id);
    Task DeleteUserCommentAsync(int id, string userId);
    Task ReplyCommentAsync(int id, string replyText);
    Task UpdateCommentAsync(BlogComment comment);
}
