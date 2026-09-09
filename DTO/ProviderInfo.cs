using CrideLLMApi.Helpers;

namespace CrideLLMApi.DTO;

/// <summary>
/// Represents information about a provider, including model name, API key, endpoint, streaming option, API mode, reasoning effort, and tool choice.
/// </summary>
public sealed record ProviderInfo
{
    public string? ModelName;
    public string? ApiKey;
    public Uri EndPoint;
    public bool Streaming;
    public API_MODE ApiMode;
    public REASONING_EFFORT ReasoningEffort;
    public TOOL_CHOICE ToolChoice = TOOL_CHOICE.AUTO;
    public TOOL_EXECUTION_MODE ToolExecutionMode { get; init; }
    = TOOL_EXECUTION_MODE.OPENAI_LOOP;
}
