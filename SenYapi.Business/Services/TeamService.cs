using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Core.Interfaces;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class TeamService : ITeamService
{
    private readonly IUnitOfWork _unitOfWork;

    public TeamService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<TeamMember>> GetAllTeamMembersAsync()
    {
        return await _unitOfWork.Repository<TeamMember>().GetAllAsync();
    }

    public async Task<TeamMember?> GetTeamMemberByIdAsync(int id)
    {
        return await _unitOfWork.Repository<TeamMember>().GetByIdAsync(id);
    }

    public async Task AddTeamMemberAsync(TeamMember member)
    {
        await _unitOfWork.Repository<TeamMember>().AddAsync(member);
        await _unitOfWork.CommitAsync();
    }

    public async Task UpdateTeamMemberAsync(TeamMember member)
    {
        _unitOfWork.Repository<TeamMember>().Update(member);
        await _unitOfWork.CommitAsync();
    }

    public async Task DeleteTeamMemberAsync(int id)
    {
        var item = await _unitOfWork.Repository<TeamMember>().GetByIdAsync(id);
        if (item != null)
        {
            _unitOfWork.Repository<TeamMember>().Delete(item);
            await _unitOfWork.CommitAsync();
        }
    }
}
