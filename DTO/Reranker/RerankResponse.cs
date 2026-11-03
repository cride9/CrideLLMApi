using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

public sealed record RerankResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("results")]
    public IReadOnlyList<RerankResult> Results { get; init; } = [];

    [JsonPropertyName("usage")]
    public RerankUsage? Usage { get; init; }
}

public sealed record RerankResult
{
    [JsonRequired]
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonRequired]
    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; init; }
}

public sealed record RerankUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
