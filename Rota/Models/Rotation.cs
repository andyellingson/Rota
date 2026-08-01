using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a reusable weekly rotation template containing shift definitions.
    /// </summary>
    public class Rotation
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Human-readable name of the rotation template.
        /// </summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Collection of shift definitions for this rotation. DayOfWeek indicates which weekday the shift applies to.
        /// </summary>
        [BsonElement("shifts")]
        public List<RotationShift> Shifts { get; set; } = new();
    }
}
