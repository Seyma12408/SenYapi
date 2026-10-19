using System.Collections.Generic;
using SenYapi.Entity;

namespace SenYapi.Web.Models;

public class HomeIndexViewModel
{
    public IEnumerable<ServiceItem> Services { get; set; } = new List<ServiceItem>();
    public IEnumerable<Project> Projects { get; set; } = new List<Project>();
    public IEnumerable<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    public IEnumerable<Testimonial> Testimonials { get; set; } = new List<Testimonial>();
    public IEnumerable<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
}
