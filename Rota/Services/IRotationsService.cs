using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Manages reusable rotation templates owned by a manager.
    /// </summary>
    public interface IRotationsService
    {
        Task<List<Rotation>> GetForManagerAsync(string managerCode);

        Task<Rotation?> GetByIdAsync(string rotationId, string managerCode);

        Task<Rotation> CreateAsync(Rotation rotation, string managerCode);

        Task<Rotation?> UpdateAsync(string rotationId, string managerCode, string name, IEnumerable<string> scheduleIds);

        Task<bool> DeleteAsync(string rotationId, string managerCode);
    }
}
