using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IFavoriteService
{
    Task<bool> ToggleFavoriteAsync(string userId, int projectId);
    Task<bool> IsFavoriteAsync(string userId, int projectId);
    Task<IEnumerable<FavoriteProject>> GetUserFavoritesAsync(string userId);
    Task RemoveFavoriteAsync(string userId, int projectId);
}
