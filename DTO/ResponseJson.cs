using System.Text.Json.Serialization;
namespace CrideLLMApi.DTO;

public sealed record ChatCompletionChunk(
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("object")]
    string Object,

    [property: JsonPropertyName("created")]
    long Created,

    [property: JsonPropertyName("model")]
    string Model,

    [property: JsonPropertyName("system_fingerprint")]
    string? SystemFingerprint,

    [property: JsonPropertyName("choices")]
    IReadOnlyList<ChoiceChunk> Choices
);

public sealed record ChoiceChunk(
    [property: JsonPropertyName("index")]
    int Index,

    [property: JsonPropertyName("delta")]
    Delta Delta,

    [property: JsonPropertyName("finish_reason")]
    string? FinishReason
);

public sealed record Delta(
    [property: JsonPropertyName("role")]
    string? Role,

    [property: JsonPropertyName("content")]
    string? Content,

    [property: JsonPropertyName("reasoning_content")]
    string? ReasoningContent,

    [property: JsonPropertyName("tool_calls")]
    IReadOnlyList<ToolCall>? ToolCalls
);

public sealed record ToolCall(
    [property: JsonPropertyName("index")]
    int Index,

    [property: JsonPropertyName("id")]
    string? Id,

    [property: JsonPropertyName("type")]
    string? Type,

    [property: JsonPropertyName("function")]
    ToolFunction? Function
);

public class ToolFunction
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}