using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a work item assigned to a user for a specific time range.
    /// </summary>
    public class WorkItem
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// The username of the user who owns/was assigned this work item.
        /// </summary>
        [BsonElement("username")]
        public string Username { get; set; } = null!;

        /// <summary>
        /// work item start (stored as UTC DateTime).
        /// </summary>
        [BsonElement("start")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Start { get; set; }

        /// <summary>
        /// work item end (stored as UTC DateTime).
        /// </summary>
        [BsonElement("end")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime End { get; set; }

        /// <summary>
        /// A short description or title for the work item.
        /// </summary>
        [BsonElement("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Optional notes about the work item.
        /// </summary>
        [BsonElement("notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// Optional color for the work item (CSS color string, e.g. "#1890ff").
        /// </summary>
        [BsonElement("color")]
        public string? Color { get; set; }

        /// <summary>
        /// MongoDB ObjectId (string) of the employee this work item is assigned to.
        /// Stored as the user's `_id` to avoid ambiguity when multiple users share the same username.
        /// </summary>
        [BsonElement("assignedTo")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedToUserId { get; set; }

        /// <summary>
        /// The team's unique code. Used to group all work items for
        /// a team's roster and to serve the correct schedule to linked employees.
        /// </summary>
        [BsonElement("teamCode")]
        public string? TeamCode { get; set; }

        /// <summary>
        /// The schedule ID this work item belongs to (MongoDB ObjectId string).
        /// </summary>
        [BsonElement("scheduleId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ScheduleId { get; set; }

        /// <summary>
        /// The name of the team member type required for this work item (manager-defined string).
        /// </summary>
        [BsonElement("workerType")]
        public string WorkerType { get; set; } = "General";

            /// <summary>
            /// Optional identifier linking work items that belong to the same recurring series.
            /// </summary>
            [BsonElement("seriesId")]
            [BsonRepresentation(BsonType.String)]
            public Guid? SeriesId { get; set; }

            /// <summary>
            /// Optional Work Item Template ID (MongoDB ObjectId string) that this work item belongs to.
            /// When set, this work item acts as a template and is not tied to a specific calendar date.
            /// ScheduleId should NOT be set for Work Item Template work items.
            /// </summary>
            [BsonElement("rotationId")]
            [BsonRepresentation(BsonType.ObjectId)]
            public string? RotationId { get; set; }

            /// <summary>
            /// Day of the week this work item template should be generated for.
            /// Only applicable when WorkWeekId is set (template work items).
            /// When generating actual work items from templates, the work item is placed on the day
            /// of the week matching this property, using the time from Start/End.
            /// </summary>
            [BsonElement("workDay")]
            public DayOfWeek? WorkDay { get; set; }

            /// <summary>
            /// True when this work item was auto-assigned using a worker's additional qualified types
            /// rather than their primary TeamMemberType.
            /// </summary>
            [BsonElement("autoAssignedFromAdditionalQualifiedType")]
            public bool AutoAssignedFromAdditionalQualifiedType { get; set; }

            /// <summary>
            /// Creation timestamp.
            /// </summary>
            [BsonElement("createdAt")]
            [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }
}
