using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

public sealed record ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("messages")]
    public IEnumerable<ChatMessage> Messages { get; init; }
        = [];

    /// <summary>
    /// Gets the tool definitions exposed to the model. Omitted from the request when no tool is
    /// registered, because providers reject an empty tool array.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<FunctionCallObject>? Tools { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = true;

    /// <summary>
    /// Gets the requested reasoning effort. Omitted from the request when no effort was requested.
    /// </summary>
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningEffort { get; init; }

    /// <summary>
    /// Gets the tool choice sent to the provider. Omitted from the request when no tool is
    /// registered, because providers validate the tool choice together with the tool definitions.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolChoice { get; init; }

    /// <summary>
    /// The maximum number of tokens to generate in the completion.
    /// When null, the provider's default is used.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// The sampling temperature to use. Lower values make the output more
    /// deterministic. When null, the provider's default is used.
    /// </summary>
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }

    /// <summary>
    /// Provider-specific chat template options, such as disabling the
    /// model's reasoning/thinking mode.
    /// </summary>
    [JsonPropertyName("chat_template_kwargs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatTemplateOptions? ChatTemplateKwargs { get; init; }
}

/// <summary>
/// Provider-specific chat template options passed through to the model.
/// </summary>
public sealed record ChatTemplateOptions
{
    /// <summary>
    /// When false, the model's reasoning/thinking mode is disabled.
    /// </summary>
    [JsonPropertyName("enable_thinking")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableThinking { get; init; }
}
