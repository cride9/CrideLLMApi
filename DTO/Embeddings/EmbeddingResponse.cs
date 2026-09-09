using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

public sealed record EmbeddingResponse
{
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("data")]
    public IReadOnlyList<EmbeddingData> Data { get; init; } = [];

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("usage")]
    public EmbeddingUsage? Usage { get; init; }
}

public sealed record EmbeddingData
{
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = [];

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

public sealed record EmbeddingUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
