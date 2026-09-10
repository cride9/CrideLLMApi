namespace CrideLLMApi.Helpers;

public static class EmbeddingExtensions
{
    /// <summary>
    /// Normalizes the embedding vector to have a magnitude of 1.
    /// </summary>
    /// <param name="embedding">The embedding vector.</param>
    /// <returns>The normalized embedding vector.</returns>
    public static EmbeddingVector Normalize(this EmbeddingVector embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Values.Length == 0)
            return embedding;

        double sum = 0;

        foreach (var value in embedding.Values)
            sum += value * value;

        var magnitude = Math.Sqrt(sum);

        if (magnitude == 0)
            return embedding;

        var values = new float[embedding.Values.Length];

        for (var i = 0; i < embedding.Values.Length; i++)
        {
            values[i] = (float)(embedding.Values[i] / magnitude);
        }

        return embedding with
        {
            Values = values,
            IsNormalized = true
        };
    }

    /// <summary>
    /// Truncates the embedding vector to the specified number of dimensions.
    /// </summary>
    /// <param name="embedding">The embedding vector.</param>
    /// <param name="dimensions">The number of dimensions to truncate to.</param>
    /// <returns>The truncated embedding vector.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified number of dimensions is invalid.</exception>
    public static EmbeddingVector Truncate(this EmbeddingVector embedding, int dimensions)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (dimensions <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimensions));

        if (dimensions > embedding.Dimensions)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions),
                $"Cannot truncate a {embedding.Dimensions}-dimensional vector to {dimensions} dimensions.");
        }

        if (dimensions == embedding.Dimensions)
            return embedding;

        return embedding with
        {
            Values = embedding.Values[..dimensions],

            // Important:
            // truncation changes the vector magnitude.
            IsNormalized = false
        };
    }

    /// <summary>
    /// Calculates the magnitude (length) of the embedding vector.
    /// </summary>
    /// <param name="embedding">The embedding vector.</param>
    /// <returns>The magnitude of the embedding vector.</returns>
    public static double Magnitude(this EmbeddingVector embedding)
    {
        double sum = 0;

        foreach (var value in embedding.Values)
            sum += value * value;

        return Math.Sqrt(sum);
    }

    /// <summary>
    /// Calculates the cosine similarity between two embedding vectors.
    /// </summary>
    /// <param name="left">The first embedding vector.</param>
    /// <param name="right">The second embedding vector.</param>
    /// <returns>The cosine similarity between the two vectors.</returns>
    public static double CosineSimilarity(this EmbeddingVector left, EmbeddingVector right)
    {
        ValidateSameDimensions(left, right);

        if (left.IsNormalized == true &&
            right.IsNormalized == true)
        {
            return left.DotProduct(right);
        }

        var leftMagnitude = left.Magnitude();
        var rightMagnitude = right.Magnitude();

        if (leftMagnitude == 0 ||
            rightMagnitude == 0)
            return 0;

        return left.DotProduct(right) /
               (leftMagnitude * rightMagnitude);
    }

    /// <summary>
    /// Returns a new embedding vector with the specified number of dimensions.
    /// </summary>
    /// <param name="embedding">The original embedding vector.</param>
    /// <param name="targetDimensions">The desired number of dimensions.</param>
    /// <returns>A new embedding vector with the specified number of dimensions.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the target dimensions are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target dimensions exceed the original dimensions.</exception>
    public static EmbeddingVector WithDimensions(this EmbeddingVector embedding, int targetDimensions)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (targetDimensions <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetDimensions));

        if (targetDimensions > embedding.Dimensions)
        {
            throw new InvalidOperationException(
                $"Cannot increase embedding dimensions from " +
                $"{embedding.Dimensions} to {targetDimensions}.");
        }

        if (targetDimensions == embedding.Dimensions)
            return embedding;

        return embedding.Truncate(targetDimensions);
    }

    /// <summary>
    /// Validates that two embedding vectors have the same number of dimensions.
    /// </summary>
    /// <param name="left">The first embedding vector.</param>
    /// <param name="right">The second embedding vector.</param>
    /// <returns>The dot product of the two vectors.</returns>
    public static double DotProduct(this EmbeddingVector left, EmbeddingVector right)
    {
        ValidateSameDimensions(left, right);

        double dot = 0;

        for (var i = 0; i < left.Dimensions; i++)
            dot += left.Values[i] * right.Values[i];

        return dot;
    }

    /// <summary>
    /// Calculates the Euclidean distance between two embedding vectors.
    /// </summary>
    /// <param name="left">The first embedding vector.</param>
    /// <param name="right">The second embedding vector.</param>
    /// <returns>The Euclidean distance between the two vectors.</returns>
    public static double EuclideanDistance(this EmbeddingVector left, EmbeddingVector right)
    {
        ValidateSameDimensions(left, right);

        double sum = 0;

        for (var i = 0; i < left.Dimensions; i++)
        {
            var diff = left.Values[i] - right.Values[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    /// <summary>
    /// Validates that two embedding vectors have the same number of dimensions.
    /// </summary>
    /// <param name="left">The first embedding vector.</param>
    /// <param name="right">The second embedding vector.</param>
    /// <exception cref="ArgumentException">Throws if any EmbeddingVector is null</exception>
    private static void ValidateSameDimensions(EmbeddingVector left, EmbeddingVector right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Dimensions != right.Dimensions)
        {
            throw new ArgumentException(
                $"Embedding dimensions must match. " +
                $"Left: {left.Dimensions}, Right: {right.Dimensions}.");
        }
    }
}
