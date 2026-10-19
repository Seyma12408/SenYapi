using System.Threading.Tasks;

namespace SenYapi.Business.Services;

public interface IUrlRedirectService
{
    Task AddRedirectAsync(string oldUrl, string newUrl, int statusCode = 301);
    Task<string?> CheckRedirectAsync(string currentPath);
}
