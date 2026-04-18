using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a named schedule that groups shifts, absences, and reminders.
    /// </summary>
    public class Schedule
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// The unique name of the schedule (must be unique per manager).
        /// </summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// The ID of the manager who owns this schedule.
        /// </summary>
        [BsonElement("managerId")]
        public string ManagerId { get; set; } = null!;

        /// <summary>
        /// The username of the manager who owns this schedule.
        /// </summary>
        [BsonElement("managerUsername")]
        public string ManagerUsername { get; set; } = null!;

        /// <summary>
        /// Manager's unique code — groups schedules under a manager.
        /// </summary>
        [BsonElement("managerCode")]
        public string? ManagerCode { get; set; }

        /// <summary>
        /// When the schedule was created.
        /// </summary>
        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional description for the schedule.
        /// </summary>
        [BsonElement("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this is the default/active schedule for the manager.
        /// </summary>
        [BsonElement("isDefault")]
        public bool IsDefault { get; set; }

        /// <summary>
        /// Optional list of named work week templates that can be applied to a calendar.
        /// Each WorkWeek contains zero or more shift definitions assigned to days of the week.
        /// </summary>
        [BsonElement("workWeeks")]
        public List<WorkWeek> WorkWeeks { get; set; } = new();
    }
}
