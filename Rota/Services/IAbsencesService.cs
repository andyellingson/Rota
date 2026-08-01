using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Service contract for creating, querying, updating, and deleting absences.
    /// </summary>
    public interface IAbsencesService
    {
        /// <summary>
        /// Returns absences that overlap the date range, filtered by manager code and/or user ObjectId.
        /// </summary>
        System.Threading.Tasks.Task<List<Absence>> GetAbsencesAsync(string? managerCode, string? userId, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Creates a new absence document.
        /// </summary>
        System.Threading.Tasks.Task<Absence> CreateAbsenceAsync(Absence absence);

        /// <summary>
        /// Deletes an absence owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<bool> DeleteAbsenceAsync(string id, string username);

        /// <summary>
        /// Updates an absence owned by the specified user or manager scope.
        /// </summary>
        System.Threading.Tasks.Task<Absence?> UpdateAbsenceAsync(string id, string username, string title, string? notes, DateTime startDateUtc, DateTime endDateUtc, int dayCount, string? color, string? userId, string? assignedToUserId, string? managerCode, string? startTime = null, string? endTime = null, AbsenceApprovalState approvalState = AbsenceApprovalState.Pending);
    }
}
