using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a reusable weekly rotation template containing shift definitions.
    /// </summary>
    // Ignore legacy elements (e.g. 'shifts') that no longer map to a property
    [BsonIgnoreExtraElements]
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

    }
}
