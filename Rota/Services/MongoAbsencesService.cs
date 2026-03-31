using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    public class MongoAbsencesService : IAbsencesService
    {
        private readonly IMongoCollection<Absence> _absences;
        private readonly ILogger<MongoAbsencesService> _logger;

        public MongoAbsencesService(IOptions<MongoDbOptions> options, ILogger<MongoAbsencesService> logger)
        {
            _logger = logger;
            var opts = options.Value;
            var client = new MongoClient(opts.ConnectionString);
            var db = client.GetDatabase(opts.DatabaseName);
            _absences = db.GetCollection<Absence>(opts.AbsencesCollectionName);
        }

        public MongoAbsencesService(IMongoCollection<Absence> collection, ILogger<MongoAbsencesService> logger)
        {
            _absences = collection;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task<List<Absence>> GetAbsencesAsync(string? managerCode, string? userId, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                // Range overlap: absence.StartDate < queryEnd AND absence.EndDate > queryStart
                var dateFilter = Builders<Absence>.Filter.And(
                    Builders<Absence>.Filter.Lt(a => a.StartDate, end),
                    Builders<Absence>.Filter.Gt(a => a.EndDate, start)
                );

                var userFilters = new List<FilterDefinition<Absence>>();
                if (!string.IsNullOrEmpty(managerCode))
                    userFilters.Add(Builders<Absence>.Filter.Eq(a => a.ManagerCode, managerCode));
                if (!string.IsNullOrEmpty(userId))
                    userFilters.Add(Builders<Absence>.Filter.Eq(a => a.UserId, userId));

                if (userFilters.Count == 0) return new List<Absence>();

                var userFilter = userFilters.Count == 1
                    ? userFilters[0]
                    : Builders<Absence>.Filter.Or(userFilters);

                return await _absences.Find(
                    Builders<Absence>.Filter.And(dateFilter, userFilter)).ToListAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving absences");
                return new List<Absence>();
            }
        }

        public async System.Threading.Tasks.Task<Absence> CreateAbsenceAsync(Absence absence)
        {
            try
            {
                absence.CreatedAt = DateTime.UtcNow;
                await _absences.InsertOneAsync(absence);
                return absence;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error creating absence for user {User}", absence?.Username);
                throw;
            }
        }

        public async System.Threading.Tasks.Task<bool> DeleteAbsenceAsync(string id, string username)
        {
            try
            {
                var filter = Builders<Absence>.Filter.And(
                    Builders<Absence>.Filter.Eq(a => a.Id, id),
                    Builders<Absence>.Filter.Eq(a => a.Username, username)
                );
                var result = await _absences.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting absence {Id}", id);
                return false;
            }
        }

        public async System.Threading.Tasks.Task<Absence?> UpdateAbsenceAsync(string id, string username, string title, string? notes, DateTime startDateUtc, DateTime endDateUtc, int dayCount, string? color, string? userId, string? forUsername, string? forDisplayName, string? managerCode)
        {
            try
            {
                var filter = Builders<Absence>.Filter.And(
                    Builders<Absence>.Filter.Eq(a => a.Id, id),
                    Builders<Absence>.Filter.Eq(a => a.Username, username)
                );
                var update = Builders<Absence>.Update
                    .Set(a => a.Title, title)
                    .Set(a => a.Notes, notes)
                    .Set(a => a.StartDate, DateTime.SpecifyKind(startDateUtc, DateTimeKind.Utc))
                    .Set(a => a.EndDate, DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc))
                    .Set(a => a.DayCount, dayCount)
                    .Set(a => a.Color, color ?? "#fa8c16")
                    .Set(a => a.UserId, userId)
                    .Set(a => a.ForUsername, forUsername)
                    .Set(a => a.ForDisplayName, forDisplayName)
                    .Set(a => a.ManagerCode, managerCode);

                var opts = new FindOneAndUpdateOptions<Absence> { ReturnDocument = ReturnDocument.After };
                return await _absences.FindOneAndUpdateAsync(filter, update, opts);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating absence {Id}", id);
                return null;
            }
        }
    }
}
