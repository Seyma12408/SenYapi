using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IProjectInquiryService
{
    Task<ProjectInquiry> CreateInquiryAsync(ProjectInquiry inquiry);
    Task<IEnumerable<ProjectInquiry>> GetUserInquiriesAsync(string userId);
    Task<IEnumerable<ProjectInquiry>> GetAllInquiriesAsync();
    Task<ProjectInquiry?> GetInquiryByIdAsync(int id);
    Task<bool> ReplyInquiryAsync(int id, string replyText, string newStatus, string adminUserName);
    Task UpdateInquiryStatusAsync(int id, string status);
}
