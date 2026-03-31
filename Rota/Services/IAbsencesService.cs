using Rota.Models;

namespace Rota.Services
{
    public interface IAbsencesService
    {
        /// <summary>
        /// Returns absences that overlap the date range, filtered by manager code and/or user ObjectId.
        /// </summary>
        System.Threading.Tasks.Task<List<Absence>> GetAbsencesAsync(string? managerCode, string? userId, DateOnly startDate, DateOnly endDate);

        System.Threading.Tasks.Task<Absence> CreateAbsenceAsync(Absence absence);

        System.Threading.Tasks.Task<bool> DeleteAbsenceAsync(string id, string username);

        System.Threading.Tasks.Task<Absence?> UpdateAbsenceAsync(string id, string username, string title, string? notes, DateTime startDateUtc, DateTime endDateUtc, int dayCount, string? color, string? userId, string? forUsername, string? forDisplayName, string? managerCode);
    }
}
