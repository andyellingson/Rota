using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    public class MongoWorkerTypesService : IWorkerTypesService
    {
        private readonly IMongoCollection<WorkerType> _workerTypes;
        private readonly ILogger<MongoWorkerTypesService> _logger;

        public MongoWorkerTypesService(IOptions<MongoDbOptions> options, ILogger<MongoWorkerTypesService> logger)
        {
            _logger = logger;
            var opts = options.Value;
            var client = new MongoClient(opts.ConnectionString);
            var db = client.GetDatabase(opts.DatabaseName);
            _workerTypes = db.GetCollection<WorkerType>(opts.WorkerTypesCollectionName);
        }

        /// <inheritdoc />
        public async Task<List<WorkerType>> GetByOwnerAsync(string ownerId)
        {
            try
            {
                return await _workerTypes
                    .Find(Builders<WorkerType>.Filter.Eq(w => w.OwnerId, ownerId))
                    .SortBy(w => w.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving worker types for owner {OwnerId}", ownerId);
                return new List<WorkerType>();
            }
        }

        /// <inheritdoc />
        public async Task<WorkerType> CreateAsync(WorkerType workerType)
        {
            try
            {
                await _workerTypes.InsertOneAsync(workerType);
                return workerType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating worker type '{Name}' for owner {OwnerId}", workerType.Name, workerType.OwnerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, string ownerId)
        {
            try
            {
                var filter = Builders<WorkerType>.Filter.And(
                    Builders<WorkerType>.Filter.Eq(w => w.Id, id),
                    Builders<WorkerType>.Filter.Eq(w => w.OwnerId, ownerId)
                );
                var result = await _workerTypes.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting worker type {Id}", id);
                return false;
            }
        }
    }
}
