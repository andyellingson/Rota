using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rota.Models
{
    /// <summary>
    /// Represents a reusable weekly Work Item Template containing work item definitions.
    /// </summary>
    // Ignore legacy elements (e.g. 'work items') that no longer map to a property
    [BsonIgnoreExtraElements]
    public class WorkItemTemplate
    {
        /// <summary>
        /// Document id (MongoDB ObjectId string representation).
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Human-readable name of the Work Item Template.
        /// </summary>
        [BsonElement("name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Team code that owns this reusable Work Item Template.
        /// </summary>
        [BsonElement("teamCode")]
        public string? TeamCode { get; set; }

        /// <summary>
        /// Schedule IDs that are allowed to use this Work Item Template.
        /// </summary>
        [BsonElement("scheduleIds")]
        public List<string> ScheduleIds { get; set; } = new();

    }
}
