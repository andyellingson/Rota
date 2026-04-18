using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a shift assigned to a user for a specific time range.
    /// </summary>
    public class Shift
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// The username of the user who owns/was assigned this shift.
        /// </summary>
        [BsonElement("username")]
        public string Username { get; set; } = null!;

        /// <summary>
        /// Shift start (stored as UTC DateTime).
        /// </summary>
        [BsonElement("start")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Start { get; set; }

        /// <summary>
        /// Shift end (stored as UTC DateTime).
        /// </summary>
        [BsonElement("end")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime End { get; set; }

        /// <summary>
        /// A short description or title for the shift.
        /// </summary>
        [BsonElement("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Optional notes about the shift.
        /// </summary>
        [BsonElement("notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// Optional color for the shift (CSS color string, e.g. "#1890ff").
        /// </summary>
        [BsonElement("color")]
        public string? Color { get; set; }

        /// <summary>
        /// MongoDB ObjectId (string) of the employee this shift is assigned to.
        /// Stored as the user's `_id` to avoid ambiguity when multiple users share the same username.
        /// </summary>
        [BsonElement("assignedTo")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedToUserId { get; set; }

        /// <summary>
        /// The manager's unique code (ManagerCode GUID). Used to group all shifts for
        /// a manager's roster and to serve the correct schedule to linked employees.
        /// </summary>
        [BsonElement("managerCode")]
        public string? ManagerCode { get; set; }

        /// <summary>
        /// The schedule ID this shift belongs to (MongoDB ObjectId string).
        /// </summary>
        [BsonElement("scheduleId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ScheduleId { get; set; }

        /// <summary>
        /// The type of worker required for the shift.
        /// </summary>
        [BsonElement("workerType")]
        public WorkerType WorkerType { get; set; } = WorkerType.General;

            /// <summary>
            /// Optional identifier linking shifts that belong to the same recurring series.
            /// </summary>
            [BsonElement("seriesId")]
            [BsonRepresentation(BsonType.String)]
            public Guid? SeriesId { get; set; }

            /// <summary>
            /// Optional WorkWeek ID (MongoDB ObjectId string) that this shift belongs to.
            /// When set, this shift acts as a template and is not tied to a specific calendar date.
            /// ScheduleId should NOT be set for workweek template shifts.
            /// </summary>
            [BsonElement("workWeekId")]
            [BsonRepresentation(BsonType.ObjectId)]
            public string? WorkWeekId { get; set; }

            /// <summary>
            /// Day of the week this shift template should be generated for.
            /// Only applicable when WorkWeekId is set (template shifts).
            /// When generating actual shifts from templates, the shift is placed on the day
            /// of the week matching this property, using the time from Start/End.
            /// </summary>
            [BsonElement("workDay")]
            public DayOfWeek? WorkDay { get; set; }

            /// <summary>
            /// Creation timestamp.
            /// </summary>
            [BsonElement("createdAt")]
            [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }
}
