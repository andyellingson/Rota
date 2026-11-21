using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Controls whether a reminder is shared with the team or limited to its recipient and manager.
    /// </summary>
    public enum ReminderVisibility
    {
        Public,
        Private
    }

    /// <summary>
    /// Represents a reminder associated with a specific date and user.
    /// </summary>
    public class Reminder
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// The date for which this reminder is scheduled.
        /// </summary>
        [BsonElement("date")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Date { get; set; }

        [BsonIgnore]
        public DateTime ReminderDateTime
        {
            get => Date;
            set => Date = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        /// <summary>
        /// The reminder title/description.
        /// </summary>
        [BsonElement("title")]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Optional detailed notes for the reminder.
        /// </summary>
        [BsonElement("notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// Optional color for the reminder (CSS color string, e.g. "#ffeb3b").
        /// </summary>
        [BsonElement("color")]
        public string? Color { get; set; } = "#ffeb3b";

        /// <summary>
        /// Team code. Set when a manager creates a reminder so all linked
        /// employees can see it on their calendar.
        /// </summary>
        [BsonElement("teamCode")]
        public string? TeamCode { get; set; }

        /// <summary>
        /// Visibility level; missing values in existing records default to Public.
        /// </summary>
        [BsonElement("visibility")]
        [BsonRepresentation(BsonType.String)]
        public ReminderVisibility Visibility { get; set; } = ReminderVisibility.Public;

        /// <summary>
        /// The schedule ID this reminder belongs to (MongoDB ObjectId string).
        /// </summary>
        [BsonElement("scheduleId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ScheduleId { get; set; }

        /// <summary>
        /// MongoDB ObjectId of the user this reminder is for.
        /// </summary>
        [BsonElement("ownerId")]
        public string? OwnerId { get; set; }

        /// <summary>
        /// MongoDB ObjectId of the user who created this reminder.
        /// </summary>
        [BsonElement("createdByUserId")]
        public string? CreatedByUserId { get; set; }

        /// <summary>
        /// Username of the worker or manager this reminder is for.
        /// </summary>
        [BsonElement("forUsername")]
        public string? ForUsername { get; set; }

        /// <summary>
        /// Display name of the worker or manager this reminder is for.
        /// </summary>
        [BsonElement("forDisplayName")]
        public string? ForDisplayName { get; set; }

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
