namespace CrideLLMApi.VectorDB;

public interface IVectorCollection
{
    string Name { get; }

    /// <summary>
    /// Upserts a vector record into the collection. If a record with the same ID already exists, it will be updated; otherwise, a new record will be inserted.
    /// </summary>
    /// <param name="record">The vector record to upsert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertAsync(
        VectorRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts multiple vector records into the collection. If a record with the same ID already exists, it will be updated; otherwise, a new record will be inserted.
    /// </summary>
    /// <param name="records">The vector records to upsert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertAsync(
        IEnumerable<VectorRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for the most similar vectors in the collection to the provided vector, returning a list of search results limited by the specified limit.
    /// </summary>
    /// <param name="vector">The vector to search for.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vector record from the collection by its ID. If the record does not exist, no action is taken.
    /// </summary>
    /// <param name="id">The ID of the record to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default);
}