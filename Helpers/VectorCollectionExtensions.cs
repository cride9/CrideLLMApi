using CrideLLMApi.Helpers;

namespace CrideLLMApi.VectorDB;

public static class VectorCollectionExtensions
{
    /// <summary>
    /// Upserts a vector record into the collection using an embedding vector.
    /// </summary>
    /// <param name="collection">The vector collection.</param>
    /// <param name="id">The ID of the vector record.</param>
    /// <param name="embedding">The embedding vector.</param>
    /// <param name="document">The document associated with the vector record.</param>
    /// <param name="metadata">The metadata for the vector record.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static Task UpsertAsync(
        this IVectorCollection collection,
        string id,
        EmbeddingVector embedding,
        string? document = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(embedding);

        return collection.UpsertAsync(
            new VectorRecord
            {
                Id = id,
                Vector = embedding.Values,
                Document = document,
                Metadata =
                    metadata ??
                    new Dictionary<string, object?>()
            },
            cancellationToken);
    }

    /// <summary>
    /// Searches the vector collection using an embedding vector and returns a list of search results.
    /// </summary>
    /// <param name="collection">The vector collection.</param>
    /// <param name="embedding">The embedding vector.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        this IVectorCollection collection,
        EmbeddingVector embedding,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(embedding);

        return collection.SearchAsync(
            embedding.Values,
            limit,
            cancellationToken);
    }
}