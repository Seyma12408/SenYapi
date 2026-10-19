using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IAuditLogService
{
    Task LogAsync(string userName, string action, string entityName, string? details = null, string? ipAddress = null);
    Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 50);
}
