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
        /// The type of work the user performs (manager-defined name stored as string).
        /// Used when assigning employees to shifts.
        /// </summary>
        [BsonElement("occupation")]
        public string Occupation { get; set; } = "General";

        /// <summary>
        /// For manager accounts: a unique code (GUID) that employees use to link themselves
        /// to this manager. Generated automatically at registration. When an employee is
        /// linked to a manager, the employee's document stores the manager's ManagerCode.
        /// </summary>
        [BsonElement("managerCode")]
        public string? ManagerCode { get; set; }

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
    }
}
