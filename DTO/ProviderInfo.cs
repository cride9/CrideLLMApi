using CrideLLMApi.Helpers;

namespace CrideLLMApi.DTO;

/// <summary>
/// Represents information about a provider, including endpoint, API key, API mode, chat options, and embedding options.
/// </summary>
public sealed record ProviderInfo
{
    public required Uri EndPoint { get; init; }
    public string? ApiKey { get; init; }
    public ChatProviderOptions Chat { get; init; } = new();
    public EmbeddingProviderOptions Embeddings { get; init; } = new();
    public API_MODE ApiMode { get; set; } = API_MODE.LOCAL;
}

/// <summary>
/// Represents options for a chat provider, including model name, streaming option, reasoning effort, tool choice, and tool execution mode.
/// </summary>
public sealed record ChatProviderOptions
{
    public string? ModelName { get; init; }
    public bool Streaming { get; init; } = true;
    public REASONING_EFFORT ReasoningEffort { get; init; } = REASONING_EFFORT.NONE;
    public TOOL_CHOICE ToolChoice { get; init; } = TOOL_CHOICE.AUTO;
    public TOOL_EXECUTION_MODE ToolExecutionMode { get; init; } = TOOL_EXECUTION_MODE.OPENAI_LOOP;
}

/// <summary>
/// Represents options for an embedding provider, including model name, target dimensions, and normalization option.
/// </summary>
public sealed record EmbeddingProviderOptions
{
    public string? ModelName { get; init; }
    public int? TargetDimensions { get; init; }
    public bool Normalize { get; init; } = false;
}