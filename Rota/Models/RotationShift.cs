using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Rota.Models
{
    /// <summary>
    /// Represents a single day-specific shift definition inside a rotation template.
    /// </summary>
    public class RotationShift
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Day of week that this shift applies to (0 = Sunday ... 6 = Saturday).
        /// </summary>
        [BsonElement("dayOfWeek")]
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>
        /// Shift start time stored as HH:mm.
        /// </summary>
        [BsonElement("startTime")]
        public string StartTime { get; set; } = "09:00";

        /// <summary>
        /// Shift end time stored as HH:mm.
        /// </summary>
        [BsonElement("endTime")]
        public string EndTime { get; set; } = "17:00";

        /// <summary>
        /// Optional display title for the shift template.
        /// </summary>
        [BsonElement("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Optional display color for the shift template.
        /// </summary>
        [BsonElement("color")]
        public string? Color { get; set; }

        /// <summary>
        /// The name of the worker type required (manager-defined string).
        /// </summary>
        [BsonElement("workerType")]
        public string WorkerType { get; set; } = "General";

        /// <summary>
        /// Optional assigned worker ObjectId for pre-assigned rotation templates.
        /// </summary>
        [BsonElement("assignedToUserId")]
        public string? AssignedToUserId { get; set; }
    }
}
