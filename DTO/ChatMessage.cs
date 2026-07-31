using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

public sealed record ChatMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; init; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; init; }
}