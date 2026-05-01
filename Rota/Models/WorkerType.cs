using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a manager-defined worker type stored in MongoDB.
    /// Each manager maintains their own isolated list of worker types.
    /// </summary>
    public class WorkerType
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Display name for this worker type (e.g. "Cook", "Cashier").
        /// </summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// The MongoDB ObjectId of the manager who owns this worker type.
        /// Worker types are isolated per manager — other managers cannot see them.
        /// </summary>
        [BsonElement("ownerId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OwnerId { get; set; } = null!;
    }
}
