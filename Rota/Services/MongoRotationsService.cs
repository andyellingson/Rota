using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// MongoDB-backed storage for reusable rotations.
    /// </summary>
    public sealed class MongoRotationsService : IRotationsService
    {
        private readonly IMongoCollection<Rotation> _rotations;
        private readonly IMongoCollection<Schedule> _schedules;
        private readonly ILogger<MongoRotationsService> _logger;

        public MongoRotationsService(IOptions<MongoDbOptions> options, ILogger<MongoRotationsService> logger)
        {
            _logger = logger;
            var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException("MongoDb:ConnectionString is required.");
            }

            var client = new MongoClient(opts.ConnectionString);
            var database = client.GetDatabase(opts.DatabaseName);
            _rotations = database.GetCollection<Rotation>("rotations");
            _schedules = database.GetCollection<Schedule>("schedules");
        }

        public MongoRotationsService(IMongoCollection<Rotation> rotations, IMongoCollection<Schedule> schedules, ILogger<MongoRotationsService> logger)
        {
            _rotations = rotations;
            _schedules = schedules;
            _logger = logger;
        }

        public async Task<List<Rotation>> GetForManagerAsync(string managerCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(managerCode);
            await MigrateLegacyRotationsAsync(managerCode);

            var filter = Builders<Rotation>.Filter.Eq(rotation => rotation.ManagerCode, managerCode);
            return await _rotations.Find(filter).SortBy(rotation => rotation.Name).ToListAsync();
        }

        public async Task<Rotation?> GetByIdAsync(string rotationId, string managerCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rotationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(managerCode);

            var filter = Builders<Rotation>.Filter.And(
                Builders<Rotation>.Filter.Eq(rotation => rotation.Id, rotationId),
                Builders<Rotation>.Filter.Eq(rotation => rotation.ManagerCode, managerCode));
            return await _rotations.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Rotation> CreateAsync(Rotation rotation, string managerCode)
        {
            ArgumentNullException.ThrowIfNull(rotation);
            ArgumentException.ThrowIfNullOrWhiteSpace(managerCode);
            rotation.Id ??= MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            rotation.ManagerCode = managerCode;
            rotation.ScheduleIds = rotation.ScheduleIds.Distinct(StringComparer.Ordinal).ToList();
            await _rotations.InsertOneAsync(rotation);
            return rotation;
        }

        public async Task<Rotation?> UpdateAsync(string rotationId, string managerCode, string name, IEnumerable<string> scheduleIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rotationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(managerCode);
            ArgumentNullException.ThrowIfNull(scheduleIds);

            var filter = Builders<Rotation>.Filter.And(
                Builders<Rotation>.Filter.Eq(rotation => rotation.Id, rotationId),
                Builders<Rotation>.Filter.Eq(rotation => rotation.ManagerCode, managerCode));
            var update = Builders<Rotation>.Update
                .Set(rotation => rotation.Name, name)
                .Set(rotation => rotation.ScheduleIds, scheduleIds.Distinct(StringComparer.Ordinal).ToList());
            return await _rotations.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<Rotation>
            {
                ReturnDocument = ReturnDocument.After
            });
        }

        public async Task<bool> DeleteAsync(string rotationId, string managerCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rotationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(managerCode);

            var filter = Builders<Rotation>.Filter.And(
                Builders<Rotation>.Filter.Eq(rotation => rotation.Id, rotationId),
                Builders<Rotation>.Filter.Eq(rotation => rotation.ManagerCode, managerCode));
            var result = await _rotations.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }

        private async Task MigrateLegacyRotationsAsync(string managerCode)
        {
            var scheduleFilter = Builders<Schedule>.Filter.Eq(schedule => schedule.ManagerCode, managerCode);
            var legacySchedules = await _schedules.Find(scheduleFilter).ToListAsync();
            var legacyRotations = legacySchedules
                .Where(schedule => !string.IsNullOrEmpty(schedule.Id))
                .SelectMany(schedule => (schedule.Rotations ?? new List<Rotation>())
                    .Where(rotation => !string.IsNullOrEmpty(rotation.Id))
                    .Select(rotation => (Rotation: rotation, ScheduleId: schedule.Id!)))
                .GroupBy(item => item.Rotation.Id!, StringComparer.Ordinal);

            foreach (var group in legacyRotations)
            {
                var rotation = group.First().Rotation;
                var scheduleIds = group.Select(item => item.ScheduleId).Distinct(StringComparer.Ordinal).ToList();
                var filter = Builders<Rotation>.Filter.Eq(item => item.Id, rotation.Id);
                var update = Builders<Rotation>.Update
                    .SetOnInsert(item => item.Name, rotation.Name)
                    .SetOnInsert(item => item.ManagerCode, managerCode)
                    .SetOnInsert(item => item.ScheduleIds, scheduleIds);
                await _rotations.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
            }

            if (legacySchedules.Any(schedule => schedule.Rotations.Count > 0))
            {
                var cleanupFilter = Builders<Schedule>.Filter.And(
                    scheduleFilter,
                    Builders<Schedule>.Filter.Exists("rotations", true));
                await _schedules.UpdateManyAsync(cleanupFilter, Builders<Schedule>.Update.Unset("rotations"));
            }
        }
    }
}
