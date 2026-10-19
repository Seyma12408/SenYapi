using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface ISiteSettingService
{
    Task<SiteSetting> GetSettingsAsync();
    Task UpdateSettingsAsync(SiteSetting setting, string adminUserName);
}
