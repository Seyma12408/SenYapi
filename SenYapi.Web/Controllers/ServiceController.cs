using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;

namespace SenYapi.Web.Controllers;

public class ServiceController : Controller
{
    private readonly IServiceItemService _serviceService;

    public ServiceController(IServiceItemService serviceService)
    {
        _serviceService = serviceService;
    }

    [HttpGet]
    [Route("Service")]
    [Route("hizmetler")]
    [Route("hizmetlerimiz")]
    [Route("Service/Index")]
    public async Task<IActionResult> Index()
    {
        var services = await _serviceService.GetAllServicesAsync();
        return View(services);
    }
}
