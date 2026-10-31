using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// MongoDB-backed storage for reusable Work Item Templates.
    /// </summary>
    public sealed class MongoWorkItemTemplatesService : IWorkItemTemplatesService
    {
        private readonly IMongoCollection<WorkItemTemplate> _workItemTemplates;
        private readonly IMongoCollection<Schedule> _schedules;
        private readonly ILogger<MongoWorkItemTemplatesService> _logger;

        public MongoWorkItemTemplatesService(IOptions<MongoDbOptions> options, ILogger<MongoWorkItemTemplatesService> logger)
        {
            _logger = logger;
            var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException("MongoDb:ConnectionString is required.");
            }

            var client = new MongoClient(opts.ConnectionString);
            var database = client.GetDatabase(opts.DatabaseName);

            // Prefer the new collection name, but safely fall back to known legacy/variant names
            // so existing data remains visible during and after migration.
            var collectionNames = database.ListCollectionNames().ToList();
            var workItemTemplatesCollectionName = collectionNames.Contains("workItemTemplates")
                ? "workItemTemplates"
                : collectionNames.Contains("workitemTemplates")
                    ? "workitemTemplates"
                    : collectionNames.Contains("rotations")
                        ? "rotations"
                        : "workItemTemplates";

            if (!string.Equals(workItemTemplatesCollectionName, "workItemTemplates", StringComparison.Ordinal))
            {
                _logger.LogWarning("Using MongoDB collection '{CollectionName}' for Work Item Templates.", workItemTemplatesCollectionName);
            }

            _workItemTemplates = database.GetCollection<WorkItemTemplate>(workItemTemplatesCollectionName);
            _schedules = database.GetCollection<Schedule>("schedules");
        }

        public MongoWorkItemTemplatesService(IMongoCollection<WorkItemTemplate> workItemTemplates, IMongoCollection<Schedule> schedules, ILogger<MongoWorkItemTemplatesService> logger)
        {
            _workItemTemplates = workItemTemplates;
            _schedules = schedules;
            _logger = logger;
        }

        public async Task<List<WorkItemTemplate>> GetForTeamAsync(string teamCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(teamCode);
            await MigrateLegacyWorkItemTemplatesAsync(teamCode);

            var filter = Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.TeamCode, teamCode);
            return await _workItemTemplates.Find(filter).SortBy(workItemTemplate => workItemTemplate.Name).ToListAsync();
        }

        public async Task<WorkItemTemplate?> GetByIdAsync(string workItemTemplateId, string teamCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workItemTemplateId);
            ArgumentException.ThrowIfNullOrWhiteSpace(teamCode);

            var filter = Builders<WorkItemTemplate>.Filter.And(
                Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.Id, workItemTemplateId),
                Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.TeamCode, teamCode));
            return await _workItemTemplates.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<WorkItemTemplate> CreateAsync(WorkItemTemplate workItemTemplate, string teamCode)
        {
            ArgumentNullException.ThrowIfNull(workItemTemplate);
            ArgumentException.ThrowIfNullOrWhiteSpace(teamCode);
            workItemTemplate.Id ??= MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            workItemTemplate.TeamCode = teamCode;
            workItemTemplate.ScheduleIds = workItemTemplate.ScheduleIds.Distinct(StringComparer.Ordinal).ToList();
            await _workItemTemplates.InsertOneAsync(workItemTemplate);
            return workItemTemplate;
        }

        public async Task<WorkItemTemplate?> UpdateAsync(string workItemTemplateId, string teamCode, string name, IEnumerable<string> scheduleIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workItemTemplateId);
            ArgumentException.ThrowIfNullOrWhiteSpace(teamCode);
            ArgumentNullException.ThrowIfNull(scheduleIds);

            var filter = Builders<WorkItemTemplate>.Filter.And(
                Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.Id, workItemTemplateId),
                Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.TeamCode, teamCode));
            var update = Builders<WorkItemTemplate>.Update
                .Set(workItemTemplate => workItemTemplate.Name, name)
                .Set(workItemTemplate => workItemTemplate.ScheduleIds, scheduleIds.Distinct(StringComparer.Ordinal).ToList());
            return await _workItemTemplates.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<WorkItemTemplate>
            {
                ReturnDocument = ReturnDocument.After
            });
        }

        public async Task<bool> DeleteAsync(string workItemTemplateId, string teamCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workItemTemplateId);
            ArgumentException.ThrowIfNullOrWhiteSpace(teamCode);

            var filter = Builders<WorkItemTemplate>.Filter.And(
                Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.Id, workItemTemplateId),
                Builders<WorkItemTemplate>.Filter.Eq(workItemTemplate => workItemTemplate.TeamCode, teamCode));
            var result = await _workItemTemplates.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }

        private async Task MigrateLegacyWorkItemTemplatesAsync(string teamCode)
        {
            var scheduleFilter = Builders<Schedule>.Filter.Eq(schedule => schedule.TeamCode, teamCode);
            var legacySchedules = await _schedules.Find(scheduleFilter).ToListAsync();
            var legacyWorkItemTemplates = legacySchedules
                .Where(schedule => !string.IsNullOrEmpty(schedule.Id))
                .SelectMany(schedule => (schedule.WorkItemTemplates ?? new List<WorkItemTemplate>())
                    .Where(workItemTemplate => !string.IsNullOrEmpty(workItemTemplate.Id))
                    .Select(workItemTemplate => (WorkItemTemplate: workItemTemplate, ScheduleId: schedule.Id!)))
                .GroupBy(item => item.WorkItemTemplate.Id!, StringComparer.Ordinal);

            foreach (var group in legacyWorkItemTemplates)
            {
                var workItemTemplate = group.First().WorkItemTemplate;
                var scheduleIds = group.Select(item => item.ScheduleId).Distinct(StringComparer.Ordinal).ToList();
                var filter = Builders<WorkItemTemplate>.Filter.Eq(item => item.Id, workItemTemplate.Id);
                var update = Builders<WorkItemTemplate>.Update
                    .SetOnInsert(item => item.Name, workItemTemplate.Name)
                    .SetOnInsert(item => item.TeamCode, teamCode)
                    .SetOnInsert(item => item.ScheduleIds, scheduleIds);
                await _workItemTemplates.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
            }

            if (legacySchedules.Any(schedule => schedule.WorkItemTemplates.Count > 0))
            {
                var cleanupFilter = Builders<Schedule>.Filter.And(
                    scheduleFilter,
                    Builders<Schedule>.Filter.Exists("rotations", true));
                await _schedules.UpdateManyAsync(cleanupFilter, Builders<Schedule>.Update.Unset("rotations"));
            }
        }
    }
}
