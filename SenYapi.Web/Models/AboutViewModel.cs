using System.Collections.Generic;
using SenYapi.Entity;

namespace SenYapi.Web.Models;

public class AboutViewModel
{
    public IEnumerable<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
