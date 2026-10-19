using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;

namespace SenYapi.Web.Controllers;

public class TeamController : Controller
{
    private readonly ITeamService _teamService;

    public TeamController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet]
    [Route("Team")]
    [Route("ekibimiz")]
    [Route("Team/Index")]
    public async Task<IActionResult> Index()
    {
        var team = await _teamService.GetAllTeamMembersAsync();
        return View(team);
    }
}
