using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Web.Models;

namespace SenYapi.Web.Controllers;

public class HomeController : Controller
{
    private readonly IServiceItemService _serviceService;
    private readonly IProjectService _projectService;
    private readonly ITeamService _teamService;
    private readonly ITestimonialService _testimonialService;
    private readonly IBlogService _blogService;

    public HomeController(
        IServiceItemService serviceService,
        IProjectService projectService,
        ITeamService teamService,
        ITestimonialService testimonialService,
        IBlogService blogService)
    {
        _serviceService = serviceService;
        _projectService = projectService;
        _teamService = teamService;
        _testimonialService = testimonialService;
        _blogService = blogService;
    }

    [HttpGet]
    [Route("")]
    [Route("Home")]
    [Route("anasayfa")]
    [Route("Home/Index")]
    public async Task<IActionResult> Index()
    {
        var model = new HomeIndexViewModel
        {
            Services = await _serviceService.GetAllServicesAsync(),
            Projects = await _projectService.GetAllProjectsAsync(onlyActive: true),
            TeamMembers = await _teamService.GetAllTeamMembersAsync(),
            Testimonials = await _testimonialService.GetAllTestimonialsAsync(),
            BlogPosts = await _blogService.GetAllPostsAsync(onlyPublished: true)
        };

        return View(model);
    }

    [HttpGet]
    [Route("gizlilik-politikasi")]
    [Route("Home/Privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    [Route("kullanim-kosullari")]
    [Route("Home/Terms")]
    public IActionResult Terms()
    {
        return View();
    }

    [HttpGet]
    [Route("cerez-politikasi")]
    [Route("Home/Cookies")]
    public IActionResult Cookies()
    {
        return View();
    }

    [HttpGet]
    [Route("sss")]
    [Route("Home/Faq")]
    public IActionResult Faq()
    {
        return View();
    }

    [HttpGet]
    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var projects = await _projectService.GetAllProjectsAsync(onlyActive: true);
        var posts = await _blogService.GetAllPostsAsync(onlyPublished: true);

        var xml = new System.Text.StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // Static routes
        string[] staticRoutes = { "", "/hakkimizda", "/hizmetler", "/projeler", "/ekibimiz", "/blog", "/iletisim", "/kullanim-kosullari", "/gizlilik-politikasi", "/cerez-politikasi", "/sss" };
        foreach (var r in staticRoutes)
        {
            xml.AppendLine($"  <url><loc>{baseUrl}{r}</loc><changefreq>weekly</changefreq><priority>0.8</priority></url>");
        }

        // Projects
        foreach (var p in projects)
        {
            var slug = string.IsNullOrEmpty(p.Slug) ? p.Id.ToString() : p.Slug;
            xml.AppendLine($"  <url><loc>{baseUrl}/projeler/{slug}</loc><changefreq>weekly</changefreq><priority>0.9</priority></url>");
        }

        // Blog posts
        foreach (var b in posts)
        {
            var slug = string.IsNullOrEmpty(b.Slug) ? b.Id.ToString() : b.Slug;
            xml.AppendLine($"  <url><loc>{baseUrl}/blog/{slug}</loc><changefreq>monthly</changefreq><priority>0.7</priority></url>");
        }

        xml.AppendLine("</urlset>");
        return Content(xml.ToString(), "application/xml", System.Text.Encoding.UTF8);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
