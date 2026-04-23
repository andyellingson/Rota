using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    public class Rotation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Collection of shift definitions for this rotation. DayOfWeek indicates which weekday the shift applies to.
        /// </summary>
        [BsonElement("shifts")]
        public List<RotationShift> Shifts { get; set; } = new();
    }
}
