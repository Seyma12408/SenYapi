using System.Collections.Generic;
using SenYapi.Entity;

namespace SenYapi.Web.Models;

public class AdminTrashViewModel
{
    public IEnumerable<Project> Projects { get; set; } = new List<Project>();
    public IEnumerable<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
}
