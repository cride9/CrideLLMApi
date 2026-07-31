using CrideLLMApi.Helpers;

namespace CrideLLMApi.DTO;

public sealed record ProviderInfo
{
    public string? ModelName;
    public string? ApiKey;
    public Uri EndPoint;
    public bool Streaming;
    public API_MODE ApiMode;
    public REASONING_EFFORT ReasoningEffort;
    public TOOL_CHOICE ToolChoice = TOOL_CHOICE.AUTO;
}
