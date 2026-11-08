using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Rota.Models
{
    /// <summary>
    /// Represents an application user stored in MongoDB.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Username used for authentication.
        /// </summary>
        [BsonElement("username")]
        public string Username { get; set; } = null!;

        /// <summary>
        /// Password hash (bcrypt).
        /// </summary>
        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = null!;

        /// <summary>
        /// Roles assigned to the user.
        /// </summary>
        [BsonElement("roles")]
        public string[] Roles { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Optional human-friendly display name shown in the UI.
        /// </summary>
        [BsonElement("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// The user's first name.
        /// </summary>
        [BsonElement("firstName")]
        public string? FirstName { get; set; }

        /// <summary>
        /// The user's last name.
        /// </summary>
        [BsonElement("lastName")]
        public string? LastName { get; set; }

        /// <summary>
        /// The team member type for the user (manager-defined name stored as string).
        /// Used when assigning employees to work items.
        /// </summary>
        [BsonElement("teamMemberType")]
        public string TeamMemberType { get; set; } = "General";

        /// <summary>
        /// For manager accounts: a unique code (GUID) that employees use to link themselves
        /// to this manager. Generated automatically at registration. When an employee is
        /// linked to a manager, the employee's document stores the manager's TeamCode.
        /// </summary>
        [BsonElement("teamCode")]
        public string? TeamCode { get; set; }

        /// <summary>
        /// The user's personal weekly availability windows, embedded directly in the user document.
        /// Each entry describes a day of the week and the start/end times the user is available.
        /// </summary>
        [BsonElement("availability")]
        public List<UserAvailability> Availability { get; set; } = new();

        /// <summary>
        /// The user's preferred UI theme. Supported values are "light" and "dark".
        /// Defaults to "light".
        /// </summary>
        [BsonElement("theme")]
        public string Theme { get; set; } = "light";

        /// <summary>
        /// The maximum number of hours the user may be scheduled for in a given week.
        /// This is set by the user's manager. A value of 0 means "no limit".
        /// </summary>
        [BsonElement("maxHours")]
        public double MaxHours { get; set; } = 0.0;

        /// <summary>
        /// work item assignment priority set by the manager. Higher numbers mean higher priority.
        /// When two employees share the same priority they are assigned work items in round-robin
        /// order. When priorities differ, the highest-priority employee is filled first (up to
        /// their weekly MaxHours cap), then the next highest, and so on.
        /// Defaults to 0 (lowest/equal priority).
        /// </summary>
        [BsonElement("workItemAssignmentPriority")]
        public int WorkItemAssignmentPriority { get; set; } = 0;

        /// <summary>
        /// Additional team member types this user is qualified to cover beyond their primary
        /// <see cref="TeamMemberType"/>.
        /// </summary>
        [BsonElement("availableTeamMemberTypes")]
        public List<string> AvailableTeamMemberTypes { get; set; } = new();

        /// <summary>
        /// For manager accounts, determines whether auto-assign can place more than one work item
        /// on the same day for a given employee. Defaults to false.
        /// </summary>
        [BsonElement("autoAssignAllowMultiplePerDay")]
        public bool AutoAssignAllowMultiplePerDay { get; set; } = false;

        /// <summary>
        /// For manager accounts, determines whether auto-assign enforces each employee's weekly
        /// MaxHours cap. Defaults to true.
        /// </summary>
        [BsonElement("autoAssignRespectMaxHours")]
        public bool AutoAssignRespectMaxHours { get; set; } = true;

        /// <summary>
        /// For manager accounts, determines whether auto-assign requires each work item to fit
        /// within the employee's saved availability windows. Defaults to true.
        /// </summary>
        [BsonElement("autoAssignRespectAvailability")]
        public bool AutoAssignRespectAvailability { get; set; } = true;

        /// <summary>
        /// For manager accounts, determines whether approved absences block auto-assignment.
        /// Defaults to true.
        /// </summary>
        [BsonElement("autoAssignRespectApprovedAbsences")]
        public bool AutoAssignRespectApprovedAbsences { get; set; } = true;

        /// <summary>
        /// For manager accounts, enables using workers' <see cref="AvailableTeamMemberTypes"/>
        /// to fill remaining unassigned work items after primary-role assignment.
        /// </summary>
        [BsonElement("autoAssignEnableAdditionalRoleCoverage")]
        public bool AutoAssignEnableAdditionalRoleCoverage { get; set; } = true;

        /// <summary>
        /// For manager accounts, when enabled the scheduler first attempts primary-role assignment,
        /// then runs additional-role coverage only for work items that remain unassigned.
        /// </summary>
        [BsonElement("autoAssignAdditionalRoleCoverageFallbackOnly")]
        public bool AutoAssignAdditionalRoleCoverageFallbackOnly { get; set; } = true;

        /// <summary>
        /// IDs of the schedules the manager has assigned to this employee.
        /// Employees can only view schedules listed here.
        /// </summary>
        [BsonElement("assignedScheduleIds")]
        public List<string> AssignedScheduleIds { get; set; } = new();
    }
}
