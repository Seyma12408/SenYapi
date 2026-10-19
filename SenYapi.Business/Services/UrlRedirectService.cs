using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class UrlRedirectService : IUrlRedirectService
{
    private readonly SenYapiDbContext _context;

    public UrlRedirectService(SenYapiDbContext context)
    {
        _context = context;
    }

    public async Task AddRedirectAsync(string oldUrl, string newUrl, int statusCode = 301)
    {
        var cleanOld = oldUrl.Trim().ToLowerInvariant();
        var cleanNew = newUrl.Trim().ToLowerInvariant();

        if (cleanOld == cleanNew) return;

        // 1. Loop prevention: if cleanNew was registered as an old URL, remove it since cleanNew is now active
        var loopCandidate = await _context.UrlRedirects.FirstOrDefaultAsync(r => r.OldUrl == cleanNew);
        if (loopCandidate != null)
        {
            _context.UrlRedirects.Remove(loopCandidate);
        }

        // 2. Chain prevention: if any previous redirect pointed to cleanOld, flatten it to point to cleanNew
        var chained = await _context.UrlRedirects.Where(r => r.NewUrl == cleanOld).ToListAsync();
        foreach (var ch in chained)
        {
            if (ch.OldUrl != cleanNew) // Avoid cycle
            {
                ch.NewUrl = cleanNew;
                ch.StatusCode = statusCode;
                ch.IsActive = true;
            }
            else
            {
                _context.UrlRedirects.Remove(ch);
            }
        }

        // 3. Upsert direct redirect
        var existing = await _context.UrlRedirects.FirstOrDefaultAsync(r => r.OldUrl == cleanOld);
        if (existing != null)
        {
            existing.NewUrl = cleanNew;
            existing.StatusCode = statusCode;
            existing.IsActive = true;
            _context.UrlRedirects.Update(existing);
        }
        else
        {
            await _context.UrlRedirects.AddAsync(new UrlRedirect
            {
                OldUrl = cleanOld,
                NewUrl = cleanNew,
                StatusCode = statusCode,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<string?> CheckRedirectAsync(string currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath)) return null;
        var cleanPath = currentPath.Trim().ToLowerInvariant();
        var redirect = await _context.UrlRedirects.AsNoTracking().FirstOrDefaultAsync(r => r.OldUrl == cleanPath && r.IsActive);
        return redirect?.NewUrl;
    }
}
