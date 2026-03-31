namespace Rota.Models
{
    /// <summary>
    /// Defines the role strings used throughout the application for authorisation.
    /// These values must match exactly what is stored in the user's Roles array in MongoDB.
    /// </summary>
    public static class Roles
    {
        /// <summary>Full administrative access, including the Admin panel.</summary>
        public const string Admin = "admin";

        /// <summary>Managerial access, including the Manage tab.</summary>
        public const string Manager = "manager";

        /// <summary>Standard employee access — calendar and reminders only.</summary>
        public const string Employee = "employee";

        /// <summary>Comma-separated list accepted by [Authorize(Roles=...)] and AuthorizeView for pages visible to both managers and admins.</summary>
        public const string ManagerOrAdmin = Manager + "," + Admin;
    }
}
