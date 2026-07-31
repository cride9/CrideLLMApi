using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace CrideLLMApi.DTO;

public class ApiFunction
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Dictionary<string, FunctionProperties> Properties { get; set; } = new();
    public string[] Required { get; set; }

    public FunctionCallObject AsFunctionCall()
    {
        FunctionCallObject dummy = new FunctionCallObject()
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
    public string Type { get; init; }
    [JsonPropertyName("description")]
    public string Description { get; init; }
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
