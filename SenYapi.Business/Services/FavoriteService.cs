using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class FavoriteService : IFavoriteService
{
    private readonly SenYapiDbContext _context;

    public FavoriteService(SenYapiDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ToggleFavoriteAsync(string userId, int projectId)
    {
        var existing = await _context.FavoriteProjects
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ProjectId == projectId);

        if (existing != null)
        {
            _context.FavoriteProjects.Remove(existing);
            await _context.SaveChangesAsync();
            return false; // Removed
        }
        else
        {
            var fav = new FavoriteProject
            {
                UserId = userId,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            try
            {
                await _context.FavoriteProjects.AddAsync(fav);
                await _context.SaveChangesAsync();
                return true; // Added
            }
            catch (DbUpdateException)
            {
                // Benzersiz indeks kısıtlaması nedeniyle eşzamanlı isteklerde kayıt zaten eklenmişse hata verme
                return true;
            }
        }
    }

    public async Task<bool> IsFavoriteAsync(string userId, int projectId)
    {
        return await _context.FavoriteProjects.AnyAsync(f => f.UserId == userId && f.ProjectId == projectId);
    }

    public async Task<IEnumerable<FavoriteProject>> GetUserFavoritesAsync(string userId)
    {
        return await _context.FavoriteProjects
            .Include(f => f.Project)
                .ThenInclude(p => p!.Images)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task RemoveFavoriteAsync(string userId, int projectId)
    {
        var fav = await _context.FavoriteProjects
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ProjectId == projectId);
        if (fav != null)
        {
            _context.FavoriteProjects.Remove(fav);
            await _context.SaveChangesAsync();
        }
    }
}
