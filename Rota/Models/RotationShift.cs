using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Rota.Models
{
    public class RotationShift
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Day of week that this shift applies to (0 = Sunday ... 6 = Saturday).
        /// </summary>
        [BsonElement("dayOfWeek")]
        public DayOfWeek DayOfWeek { get; set; }

        [BsonElement("startTime")]
        public string StartTime { get; set; } = "09:00"; // stored as HH:mm

        [BsonElement("endTime")]
        public string EndTime { get; set; } = "17:00"; // stored as HH:mm

        [BsonElement("title")]
        public string? Title { get; set; }

        [BsonElement("color")]
        public string? Color { get; set; }

        /// <summary>
        /// The name of the worker type required (manager-defined string).
        /// </summary>
        [BsonElement("workerType")]
        public string WorkerType { get; set; } = "General";

        [BsonElement("assignedToUserId")]
        public string? AssignedToUserId { get; set; }
    }
}
