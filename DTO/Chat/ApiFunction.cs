using System.Text.Json.Serialization;
namespace CrideLLMApi.DTO;

/// <summary>
/// Represents an API function (tool) that is exposed to the model.
/// <para>
/// The same <see cref="Name"/> can only be registered once per conversation, because the model
/// calls tools by name.
/// </para>
/// </summary>
public class ApiFunction
{
    /// <summary>
    /// Gets or sets the function name the model uses to call the tool. Required and must be unique.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description shown to the model. Omitted from the request when null.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema properties of the function parameters.
    /// </summary>
    public Dictionary<string, FunctionProperties> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets the names of the parameters the model must provide. Omitted from the request when null.
    /// </summary>
    public string[]? Required { get; set; }

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

/// <summary>
/// Describes a single JSON schema property of a tool parameter.
/// </summary>
public sealed record FunctionProperties
{
    /// <summary>
    /// Gets the JSON schema type of the property, for example <c>string</c> or <c>number</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the description of the property shown to the model.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the allowed values of the property.
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object[ ]? Enum { get; init; }
}

/// <summary>
/// Represents a tool definition in the OpenAI-compatible request format.
/// </summary>
public sealed record FunctionCallObject
{
    /// <summary>
    /// Gets the tool type. Always <c>function</c> for the supported providers.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the function body exposed to the model.
    /// </summary>
    [JsonPropertyName("function")]
    public required FunctionBody Function { get; init; }
}

/// <summary>
/// Contains the name, description and parameter schema of a tool.
/// </summary>
public sealed record FunctionBody
{
    /// <summary>
    /// Gets the function name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the function description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the parameter schema of the function.
    /// </summary>
    [JsonPropertyName("parameters")]
    public required FunctionParameters Parameters { get; init; }
}

/// <summary>
/// Describes the JSON schema object of a tool's parameters.
/// </summary>
public sealed record FunctionParameters
{
    /// <summary>
    /// Gets the schema type. Always <c>object</c> for tool parameters.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the declared properties of the parameter object.
    /// </summary>
    [JsonPropertyName("properties")]
    public required Dictionary<string, FunctionProperties> Properties { get; init; }

    /// <summary>
    /// Gets the names of the required parameters. Omitted from the request when null.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Required { get; init; }
}
