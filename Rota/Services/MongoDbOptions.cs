namespace Rota.Services
{
    public class MongoDbOptions
    {
        /// <summary>
        /// Connection string used to connect to MongoDB.
        /// </summary>
        public string ConnectionString { get; set; } = null!;
        /// <summary>
        /// Name of the MongoDB database to use.
        /// </summary>
        public string DatabaseName { get; set; } = "RotaDb";
        /// <summary>
        /// Name of the collection that stores user documents.
        /// </summary>
        public string UsersCollectionName { get; set; } = "users";
        /// <summary>
        /// Name of the collection that stores reminder documents.
        /// </summary>
        public string RemindersCollectionName { get; set; } = "reminders";
        /// <summary>
        /// Name of the collection that stores shift documents.
        /// </summary>
        public string ShiftsCollectionName { get; set; } = "shifts";

        /// <summary>
        /// Name of the collection that stores absence documents.
        /// </summary>
        public string AbsencesCollectionName { get; set; } = "absences";

        /// <summary>
        /// Name of the collection that stores manager-defined worker type documents.
        /// </summary>
        public string WorkerTypesCollectionName { get; set; } = "workerTypes";
    }
}
