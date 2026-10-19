using System.Collections.Generic;
using SenYapi.Entity;

namespace SenYapi.Web.Models;

public class AdminDashboardViewModel
{
    public int TotalMessages { get; set; }
    public int UnreadMessages { get; set; }
    public int TotalSubscribers { get; set; }
    public int TotalProjects { get; set; }
    public int TotalServices { get; set; }
    public int TotalTeamMembers { get; set; }
    public int TotalBlogPosts { get; set; }

    public int PendingInquiries { get; set; }
    public int PendingAppointments { get; set; }
    public int OpenConversations { get; set; }
    public int PendingComments { get; set; }

    public IEnumerable<ContactMessage> Messages { get; set; } = new List<ContactMessage>();
    public IEnumerable<NewsletterSubscriber> Subscribers { get; set; } = new List<NewsletterSubscriber>();
    public IEnumerable<Project> Projects { get; set; } = new List<Project>();
    public IEnumerable<ServiceItem> Services { get; set; } = new List<ServiceItem>();
    public IEnumerable<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    public IEnumerable<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
    public IEnumerable<ProjectInquiry> RecentInquiries { get; set; } = new List<ProjectInquiry>();
    public IEnumerable<ProjectAppointment> RecentAppointments { get; set; } = new List<ProjectAppointment>();
}
