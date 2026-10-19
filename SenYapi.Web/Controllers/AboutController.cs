using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Web.Models;

namespace SenYapi.Web.Controllers;

public class AboutController : Controller
{
    private readonly ITeamService _teamService;

    public AboutController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet]
    [Route("About")]
    [Route("hakkimizda")]
    [Route("About/Index")]
    public async Task<IActionResult> Index()
    {
        var model = new AboutViewModel
        {
            TeamMembers = await _teamService.GetAllTeamMembersAsync()
        };
        return View(model);
    }
}
