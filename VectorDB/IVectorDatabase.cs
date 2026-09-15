namespace CrideLLMApi.VectorDB;

public interface IVectorDatabase
{
    /// <summary>
    /// Gets or creates a vector collection with the specified name. If the collection does not exist, it will be created.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<IVectorCollection> GetOrCreateCollectionAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vector collection with the specified name. If the collection does not exist, no action is taken.
    /// </summary>
    /// <param name="name">The name of the collection to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteCollectionAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of all existing vector collection names in the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<IReadOnlyList<string>> GetCollectionsAsync(
        CancellationToken cancellationToken = default);
}