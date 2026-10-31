using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Manages reusable Work Item Templates owned by a team.
    /// </summary>
    public interface IWorkItemTemplatesService
    {
        Task<List<WorkItemTemplate>> GetForTeamAsync(string teamCode);

        Task<WorkItemTemplate?> GetByIdAsync(string workItemTemplateId, string teamCode);

        Task<WorkItemTemplate> CreateAsync(WorkItemTemplate workItemTemplate, string teamCode);

        Task<WorkItemTemplate?> UpdateAsync(string workItemTemplateId, string teamCode, string name, IEnumerable<string> scheduleIds);

        Task<bool> DeleteAsync(string workItemTemplateId, string teamCode);
    }
}
