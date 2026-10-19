using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class AuditLogService : IAuditLogService
{
    private readonly SenYapiDbContext _context;

    public AuditLogService(SenYapiDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string userName, string action, string entityName, string? details = null, string? ipAddress = null)
    {
        var log = new AuditLog
        {
            UserName = userName,
            Action = action,
            EntityName = entityName,
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };
        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 50)
    {
        return await _context.AuditLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
