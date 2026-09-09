namespace CrideLLMApi.Helpers;

public sealed record EmbeddingVector
{
    public required float[] Values { get; init; }

    public string? Model { get; init; }

    public int OriginalDimensions { get; init; }

    public bool IsNormalized { get; init; }

    public int Dimensions => Values.Length;

    public static implicit operator float[](EmbeddingVector embedding)
        => embedding.Values;
}
