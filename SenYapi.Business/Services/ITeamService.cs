using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface ITeamService
{
    Task<IEnumerable<TeamMember>> GetAllTeamMembersAsync();
    Task<TeamMember?> GetTeamMemberByIdAsync(int id);
    Task AddTeamMemberAsync(TeamMember member);
    Task UpdateTeamMemberAsync(TeamMember member);
    Task DeleteTeamMemberAsync(int id);
}
