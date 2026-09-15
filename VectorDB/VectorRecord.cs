namespace CrideLLMApi.VectorDB;

public sealed record VectorRecord
{
    public required string Id { get; init; }

    public required float[] Vector { get; init; }

    public string? Document { get; init; }

    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();
}