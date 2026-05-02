using Rota.Models;

namespace Rota.Services
{
    public interface IUserService
    {
        /// <summary>
        /// Finds a user by username.
        /// Returns null when the user is not found.
        /// </summary>
        System.Threading.Tasks.Task<User?> GetByUsernameAsync(string username);

        /// <summary>
        /// Validates provided credentials for the specified username.
        /// Returns true when credentials are valid; otherwise false.
        /// </summary>
        System.Threading.Tasks.Task<bool> ValidateCredentialsAsync(string username, string password);

        /// <summary>
        /// Creates a new user record and stores a hashed password.
        /// </summary>
        System.Threading.Tasks.Task CreateUserAsync(User user, string password);

        /// <summary>
        /// Returns all users that have the specified role string in their Roles array.
        /// </summary>
        System.Threading.Tasks.Task<List<User>> GetUsersByRoleAsync(string role);

        /// <summary>
        /// Updates the display name for the specified user.
        /// Returns true when the document was modified.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdateDisplayNameAsync(string username, string? displayName);

        /// <summary>
        /// Updates the occupation for the specified user.
        /// Returns true when the document was modified.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdateOccupationAsync(string username, string occupation);

        /// <summary>
        /// Updates the user's first and last name fields.
        /// Returns true when the document was modified.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdateNamesAsync(string username, string? firstName, string? lastName);

        /// <summary>
        /// Updates the maximum weekly hours for the specified user.
        /// Returns true when the document was modified.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdateMaxHoursAsync(string username, double maxHours);

        /// <summary>
        /// Verifies <paramref name="currentPassword"/> then replaces the stored hash with a
        /// hash of <paramref name="newPassword"/>.
        /// Returns true on success, false when the current password is wrong.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdatePasswordAsync(string username, string currentPassword, string newPassword);

        /// <summary>
        /// Finds a manager by their unique ManagerCode.
        /// Returns null when no manager with that code exists.
        /// </summary>
        System.Threading.Tasks.Task<User?> GetByManagerCodeAsync(string code);

        /// <summary>
        /// Links an employee to the manager identified by <paramref name="managerCode"/>.
        /// Returns true on success, false when the code does not match any manager.
        /// </summary>
        System.Threading.Tasks.Task<bool> LinkToManagerAsync(string employeeUsername, string managerCode);

        /// <summary>
        /// Returns the manager and all linked users for the specified manager code.
        /// ManagerCode is the GUID-like code owned by managers and stored on linked users.
        /// </summary>
        System.Threading.Tasks.Task<List<User>> GetLinkedUsersForManagerAsync(string managerCode);

        /// <summary>
        /// Replaces the user's embedded availability array with the supplied list.
        /// Returns true when the document was modified.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdateAvailabilityAsync(string username, List<Rota.Models.UserAvailability> availability);

        /// <summary>
        /// Updates the theme preference for the specified user.
        /// Returns true when the document was modified.
        /// </summary>
        System.Threading.Tasks.Task<bool> UpdateThemeAsync(string username, string theme);
    }
}
