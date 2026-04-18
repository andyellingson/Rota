using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a single availability window for a user on a given day of the week.
    /// Embedded as an array inside the User document.
    /// </summary>
    public class UserAvailability
    {
        /// <summary>
        /// Client-side identifier used for list management. Not stored as a MongoDB ObjectId.
        /// </summary>
        [BsonElement("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The day of the week this availability applies to.
        /// </summary>
        [BsonElement("dayOfWeek")]
        [BsonRepresentation(BsonType.String)]
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>
        /// Start of the availability window, stored as "HH:mm".
        /// </summary>
        [BsonElement("startTime")]
        public string StartTime { get; set; } = "09:00";

        /// <summary>
        /// End of the availability window, stored as "HH:mm".
        /// </summary>
        [BsonElement("endTime")]
        public string EndTime { get; set; } = "17:00";
    }
}
