using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Rota.Models;
using System.Collections.Generic;

namespace Rota.Services
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
        /// The type of work the user performs.
        /// Used when assigning employees to shifts.
        /// </summary>
        [BsonElement("occupation")]
        [BsonRepresentation(BsonType.String)]
        public WorkerType Occupation { get; set; } = WorkerType.General;

        /// <summary>
        /// For employee accounts: the username of the manager responsible for this user.
        /// </summary>
        [BsonElement("managerUsername")]
        public string? ManagerUsername { get; set; }

        /// <summary>
        /// For manager accounts: a unique code (GUID) that employees use to link themselves
        /// to this manager. Generated automatically at registration.
        /// </summary>
        [BsonElement("managerCode")]
        public string? ManagerCode { get; set; }

        /// <summary>
        /// The user's personal weekly availability windows, embedded directly in the user document.
        /// Each entry describes a day of the week and the start/end times the user is available.
        /// </summary>
        [BsonElement("availability")]
        public List<UserAvailability> Availability { get; set; } = new();
    }
}
