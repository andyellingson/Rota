using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    public class WorkWeek
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Collection of shift definitions for this work week. DayOfWeek indicates which weekday the shift applies to.
        /// </summary>
        [BsonElement("shifts")]
        public List<WorkWeekShift> Shifts { get; set; } = new();
    }
}
