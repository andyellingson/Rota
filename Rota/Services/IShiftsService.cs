using Rota.Models;

namespace Rota.Services
{
    public interface IShiftsService
    {
        System.Threading.Tasks.Task<List<Shift>> GetShiftsAsync(string? username, string? userId, string? managerCode, DateOnly startDate, DateOnly endDate);
        System.Threading.Tasks.Task<Shift> CreateShiftAsync(Shift shift);
        System.Threading.Tasks.Task<bool> DeleteShiftAsync(string id, string username);
        System.Threading.Tasks.Task<int> DeleteShiftsBySeriesIdAsync(Guid seriesId, string username);
        System.Threading.Tasks.Task<Shift?> UpdateShiftAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, WorkerType workerType, string? color, string? assignedToUserId);
    }
}
