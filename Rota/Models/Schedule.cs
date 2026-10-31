using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a named schedule that groups work items, absences, and reminders.
    /// </summary>
    // Ignore legacy elements that no longer map to a property
    [BsonIgnoreExtraElements]
    public class Schedule
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// The unique name of the schedule (must be unique per team leader).
        /// </summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// The ID of the team leader who owns this schedule.
        /// </summary>
        [BsonElement("teamLeaderId")]
        public string TeamLeaderId { get; set; } = null!;

        /// <summary>
        /// The username of the team leader who owns this schedule.
        /// </summary>
        [BsonElement("teamLeaderUserName")]
        public string TeamLeaderUserName { get; set; } = null!;

        /// <summary>
        /// Team's unique code — groups schedules under a team leader.
        /// </summary>
        [BsonElement("teamCode")]
        public string? TeamCode { get; set; }

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
        /// Optional list of named rotation templates that can be applied to a calendar.
        /// Each Rotation contains zero or more work item definitions assigned to days of the week.
        /// </summary>
        [BsonElement("rotations")]
        public List<Rotation> Rotations { get; set; } = new();
    }
}
