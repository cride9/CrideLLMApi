using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

public sealed record RerankRequest
{
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("documents")]
    public required IReadOnlyList<string> Documents { get; init; }

    [JsonPropertyName("top_n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopN { get; init; }
}
