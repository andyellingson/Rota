using Rota.Models;

namespace Rota.Services
{
    public interface IWorkerTypesService
    {
        /// <summary>
        /// Returns all worker types that belong to the specified manager.
        /// </summary>
        Task<List<WorkerType>> GetByOwnerAsync(string ownerId);

        /// <summary>
        /// Creates a new worker type for the given manager.
        /// </summary>
        Task<WorkerType> CreateAsync(WorkerType workerType);

        /// <summary>
        /// Deletes a worker type by id, scoped to the owning manager.
        /// Returns true if a document was deleted.
        /// </summary>
        Task<bool> DeleteAsync(string id, string ownerId);
    }
}
