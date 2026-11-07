using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

/// <summary>
/// OpenAI-compatible structured output request. llama.cpp converts the JSON schema into a
/// sampling grammar, so the model cannot emit syntactically invalid or off-schema JSON.
/// Semantic rules (cross-field consistency, evidence) still require caller-side validation.
/// </summary>
public sealed record ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_schema";

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonSchemaDefinition? JsonSchema { get; init; }

    /// <summary>Creates a json_schema response format.</summary>
    /// <param name="name">Schema name ([a-zA-Z0-9_-], as required by OpenAI-compatible servers).</param>
    /// <param name="schema">The JSON schema document.</param>
    /// <param name="strict">Requests strict schema adherence where the provider supports it.</param>
    public static ResponseFormat FromJsonSchema(string name, JsonElement schema, bool strict = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ResponseFormat
        {
            JsonSchema = new JsonSchemaDefinition { Name = name, Schema = schema, Strict = strict }
        };
    }
}

public sealed record JsonSchemaDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "response";

    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }

    [JsonPropertyName("strict")]
    public bool Strict { get; init; } = true;
}
