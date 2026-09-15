namespace CrideLLMApi.Helpers;

/// <summary>
/// Represents an embedding vector together with the model that produced it and the metadata
/// needed by the vector helper methods.
/// </summary>
public sealed record EmbeddingVector
{
    /// <summary>
    /// Gets the vector values.
    /// </summary>
    public required float[] Values { get; init; }

    /// <summary>
    /// Gets the provider model name, when it was reported by the provider.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the dimensionality of the vector as returned by the provider, before any truncation.
    /// </summary>
    public int OriginalDimensions { get; init; }

    /// <summary>
    /// Gets a value indicating whether the vector is currently normalized. Truncating a
    /// normalized vector clears this flag because truncation changes the magnitude.
    /// </summary>
    public bool IsNormalized { get; init; }

    /// <summary>
    /// Gets the current number of dimensions. Returns zero when no vector values are present,
    /// so callers cannot fail with a null reference.
    /// </summary>
    public int Dimensions => Values is null ? 0 : Values.Length;

    /// <summary>
    /// Implicitly converts the embedding to its raw vector values.
    /// </summary>
    /// <param name="embedding">The embedding vector.</param>
    public static implicit operator float[](EmbeddingVector embedding)
        => embedding.Values;

    /// <summary>
    /// Gets the vector values as a span.
    /// </summary>
    public ReadOnlySpan<float> Span => Values;

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the value.</param>
    /// <returns>The value at the specified index.</returns>
    public float this[int index] => Values[index];

    /// <summary>
    /// Returns a readable description of the embedding.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString()
        => $"{Model ?? "Unknown"} embedding [{Dimensions}D]";
}
