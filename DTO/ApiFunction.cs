using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace CrideLLMApi.DTO;

/// <summary>
/// Represents an API function with its name, description, properties, and required parameters.
/// </summary>
public class ApiFunction
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Dictionary<string, FunctionProperties> Properties { get; set; } = new();
    public string[] Required { get; set; }

    /// <summary>
    /// Converts the ApiFunction instance into a FunctionCallObject, which is a representation of the function call structure.
    /// </summary>
    /// <returns>The function call object.</returns>
    public FunctionCallObject AsFunctionCall()
    {
        FunctionCallObject dummy = new()
        {
            Type = "function",
            Function = new FunctionBody()
            {
                Name = Name,
                Description = Description,
                Parameters = new FunctionParameters()
                {
                    Type = "object",
                    Properties = Properties,
                    Required = Required
                }
            }
        };

        return dummy;
    }
}

public sealed record FunctionProperties
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object[ ]? Enum { get; init; }
}

public sealed record FunctionCallObject
{
    [JsonPropertyName("type")]
    public string Type { get; init; }
    [JsonPropertyName("function")]
    public FunctionBody Function { get; init; }
}

public sealed record FunctionBody
{
    [JsonPropertyName("name")]
    public string Name { get; init; }
    [JsonPropertyName("description")]
    public string Description { get; init; }
    [JsonPropertyName("parameters")]
    public FunctionParameters Parameters { get; init; }
}

public sealed record FunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; init; }
    [JsonPropertyName("properties")]
    public Dictionary<string, FunctionProperties> Properties { get; init; }
    [JsonPropertyName("required")]
    public string[] Required { get; init; }
}
