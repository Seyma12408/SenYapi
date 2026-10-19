using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Web.Models;

namespace SenYapi.Web.Controllers.Admin;

/// <summary>
/// Admin Dashboard Controller: Yalnızca özet ekranı ve gösterge paneli verilerini yönetir.
/// </summary>
[Route("Admin")]
public class AdminController : AdminBaseController
{
    private readonly IContactService _contactService;
    private readonly INewsletterService _newsletterService;
    private readonly IProjectService _projectService;
    private readonly IServiceItemService _serviceService;
    private readonly ITeamService _teamService;
    private readonly IBlogService _blogService;
    private readonly IProjectInquiryService _inquiryService;
    private readonly IProjectAppointmentService _appointmentService;
    private readonly IConversationService _conversationService;

    public AdminController(
        IContactService contactService,
        INewsletterService newsletterService,
        IProjectService projectService,
        IServiceItemService serviceService,
        ITeamService teamService,
        IBlogService blogService,
        IProjectInquiryService inquiryService,
        IProjectAppointmentService appointmentService,
        IConversationService conversationService)
    {
        _contactService = contactService;
        _newsletterService = newsletterService;
        _projectService = projectService;
        _serviceService = serviceService;
        _teamService = teamService;
        _blogService = blogService;
        _inquiryService = inquiryService;
        _appointmentService = appointmentService;
        _conversationService = conversationService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var messages = await _contactService.GetAllMessagesAsync();
        var subscribers = await _newsletterService.GetAllSubscribersAsync();
        var projects = await _projectService.GetAllProjectsAsync();
        var services = await _serviceService.GetAllServicesAsync();
        var team = await _teamService.GetAllTeamMembersAsync();
        var blogPosts = await _blogService.GetAllPostsAsync();
        var comments = await _blogService.GetAllCommentsAsync();
        var inquiries = await _inquiryService.GetAllInquiriesAsync();
        var appointments = await _appointmentService.GetAllAppointmentsAsync();
        var conversations = await _conversationService.GetAllConversationsAsync();

        var model = new AdminDashboardViewModel
        {
            TotalMessages = messages.Count(),
            UnreadMessages = messages.Count(m => !m.IsRead),
            TotalSubscribers = subscribers.Count(),
            TotalProjects = projects.Count(),
            TotalServices = services.Count(),
            TotalTeamMembers = team.Count(),
            TotalBlogPosts = blogPosts.Count(),

            PendingInquiries = inquiries.Count(i => i.Status == "Beklemede" || i.Status == "Yeni"),
            PendingAppointments = appointments.Count(a => a.Status == "Beklemede"),
            OpenConversations = conversations.Count(c => c.Status == "Acik"),
            PendingComments = comments.Count(c => !c.IsApproved),

            Messages = messages.Take(5).ToList(),
            Subscribers = subscribers.Take(5).ToList(),
            Projects = projects.Take(5).ToList(),
            Services = services.Take(5).ToList(),
            TeamMembers = team.Take(5).ToList(),
            BlogPosts = blogPosts.Take(5).ToList(),
            RecentInquiries = inquiries.Take(5).ToList(),
            RecentAppointments = appointments.Take(5).ToList()
        };

        return View("~/Views/Admin/Index.cshtml", model);
    }
}
