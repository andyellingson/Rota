using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BCrypt.Net;

namespace Rota.Services
{
    /// <summary>
    /// MongoDB-backed implementation of <see cref="IUserService"/>.
    /// Handles creating users and validating credentials against stored bcrypt password hashes.
    /// </summary>
    public class MongoUserService : IUserService
    {
        private readonly IMongoCollection<User> _users;
        private readonly ILogger<MongoUserService> _logger;

        /// <summary>
        /// Production constructor which creates a MongoDB client using provided options.
        /// </summary>
        public MongoUserService(IOptions<MongoDbOptions> options, ILogger<MongoUserService> logger)
        {
            _logger = logger;
            var opts = options.Value;

            try
            {
                var client = new MongoClient(opts.ConnectionString);
                var db = client.GetDatabase(opts.DatabaseName);
                _users = db.GetCollection<User>(opts.UsersCollectionName);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error initializing MongoUserService with connection string {Conn}", opts.ConnectionString);
                throw;
            }
        }

        /// <summary>
        /// Test-friendly constructor that accepts an <see cref="IMongoCollection{User}"/> directly.
        /// This allows unit tests to pass a mocked collection.
        /// </summary>
        public MongoUserService(IMongoCollection<User> usersCollection, ILogger<MongoUserService> logger)
        {
            _users = usersCollection;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new user and stores a bcrypt password hash.
        /// Errors are logged and rethrown so calling code can decide how to handle them.
        /// </summary>
        public async System.Threading.Tasks.Task CreateUserAsync(User user, string password)
        {
            try
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                await _users.InsertOneAsync(user);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error creating user {User}", user?.Username);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by username. On error, logs and returns null.
        /// </summary>
        public async System.Threading.Tasks.Task<User?> GetByUsernameAsync(string username)
        {
            try
            {
                return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username {User}", username);
                return null;
            }
        }

        /// <summary>
        /// Validates a username/password pair against the stored hash. Returns false on errors.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            try
            {
                var user = await GetByUsernameAsync(username);
                if (user == null) return false;
                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for user {User}", username);
                return false;
            }
        }

        /// <summary>
        /// Returns all users that have the specified role string in their Roles array.
        /// </summary>
        public async System.Threading.Tasks.Task<List<User>> GetUsersByRoleAsync(string role)
        {
            try
            {
                var filter = Builders<User>.Filter.AnyEq(u => u.Roles, role);
                return await _users.Find(filter).SortBy(u => u.Username).ToListAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users with role {Role}", role);
                return new List<User>();
            }
        }

        /// <summary>
        /// Updates the display name for the specified user.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> UpdateDisplayNameAsync(string username, string? displayName)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Username, username);
                var update = Builders<User>.Update.Set(u => u.DisplayName, displayName);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating display name for user {User}", username);
                return false;
            }
        }

        /// <summary>
        /// Updates the occupation for the specified user.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> UpdateOccupationAsync(string username, Rota.Models.WorkerType occupation)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Username, username);
                var update = Builders<User>.Update.Set(u => u.Occupation, occupation);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating occupation for user {User}", username);
                return false;
            }
        }

        /// <summary>
        /// Verifies the current password then replaces the stored hash.
        /// Returns false when the current password does not match.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> UpdatePasswordAsync(string username, string currentPassword, string newPassword)
        {
            try
            {
                var user = await GetByUsernameAsync(username);
                if (user == null) return false;
                if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;

                var filter = Builders<User>.Filter.Eq(u => u.Username, username);
                var update = Builders<User>.Update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(newPassword));
                await _users.UpdateOneAsync(filter, update);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating password for user {User}", username);
                return false;
            }
        }

        /// <summary>
        /// Finds the manager whose ManagerCode matches the supplied value.
        /// </summary>
        public async System.Threading.Tasks.Task<User?> GetByManagerCodeAsync(string code)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.ManagerCode, code);
                return await _users.Find(filter).FirstOrDefaultAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error looking up manager by code");
                return null;
            }
        }

        /// <summary>
        /// Sets the employee's ManagerUsername to the manager identified by
        /// <paramref name="managerCode"/>. Returns false when the code is not found.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> LinkToManagerAsync(string employeeUsername, string managerCode)
        {
            try
            {
                var manager = await GetByManagerCodeAsync(managerCode);
                if (manager is null) return false;

                var filter = Builders<User>.Filter.Eq(u => u.Username, employeeUsername);
                var update = Builders<User>.Update
                    .Set(u => u.ManagerUsername, manager.Username)
                    .Set(u => u.ManagerCode, manager.ManagerCode);
                await _users.UpdateOneAsync(filter, update);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error linking employee {Employee} to manager via code", employeeUsername);
                return false;
            }
        }

        public async System.Threading.Tasks.Task<List<User>> GetLinkedUsersForManagerAsync(string managerUsername)
        {
            try
            {
                var filter = Builders<User>.Filter.Or(
                    Builders<User>.Filter.Eq(u => u.Username, managerUsername),
                    Builders<User>.Filter.Eq(u => u.ManagerUsername, managerUsername)
                );

                var users = await _users.Find(filter).ToListAsync();
                return users
                    .OrderBy(u => u.DisplayName ?? u.Username)
                    .ThenBy(u => u.Username)
                    .ToList();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving linked users for manager {Manager}", managerUsername);
                return new List<User>();
            }
        }

        /// <summary>
        /// Replaces the user's embedded availability array with the supplied list.
        /// Returns true when the document was modified.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> UpdateAvailabilityAsync(string username, List<Rota.Models.UserAvailability> availability)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Username, username);
                var update = Builders<User>.Update.Set(u => u.Availability, availability);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating availability for user {User}", username);
                return false;
            }
        }

        /// <summary>
        /// Updates the theme preference for the specified user.
        /// Returns true when the document was modified.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> UpdateThemeAsync(string username, string theme)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Username, username);
                var update = Builders<User>.Update.Set(u => u.Theme, theme);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating theme for user {User}", username);
                return false;
            }
        }
    }
}
