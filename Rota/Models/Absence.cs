using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a period of absence for a user (e.g. sick leave, holiday).
    /// </summary>
    public class Absence
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>Username of the user who logged the absence.</summary>
        [BsonElement("username")]
        public string Username { get; set; } = null!;

        /// <summary>MongoDB ObjectId of the absent user, used to fetch personal absences.</summary>
        [BsonElement("userId")]
        public string? UserId { get; set; }

        /// <summary>Manager's unique code — groups absences under a roster.</summary>
        [BsonElement("managerCode")]
        public string? ManagerCode { get; set; }

        /// <summary>The schedule ID this absence belongs to (MongoDB ObjectId string).</summary>
        [BsonElement("scheduleId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ScheduleId { get; set; }

        /// <summary>First day of the absence (UTC midnight).</summary>
        [BsonElement("startDate")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime StartDate { get; set; }

        /// <summary>Exclusive end — midnight of the day after the last absence day (UTC).</summary>
        [BsonElement("endDate")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime EndDate { get; set; }

        /// <summary>Optional local start time for part-day absences (format "HH:mm"). Only used when DayCount == 1.</summary>
        [BsonElement("startTime")]
        public string? StartTime { get; set; }

        /// <summary>Optional local end time for part-day absences (format "HH:mm"). Only used when DayCount == 1.</summary>
        [BsonElement("endTime")]
        public string? EndTime { get; set; }

        /// <summary>Number of consecutive absent days.</summary>
        [BsonElement("dayCount")]
        public int DayCount { get; set; } = 1;

        /// <summary>Short title, e.g. "Sick Leave" or "Holiday".</summary>
        [BsonElement("title")]
        public string Title { get; set; } = null!;

        /// <summary>Optional additional notes.</summary>
        [BsonElement("notes")]
        public string? Notes { get; set; }

        /// <summary>Background colour shown on the calendar (CSS color string).</summary>
        [BsonElement("color")]
        public string? Color { get; set; } = "#fa8c16";

        /// <summary>MongoDB ObjectId (string) of the worker or manager this absence is for.</summary>
        [BsonElement("assignedTo")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedToUserId { get; set; }

        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
