using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Tools;

public sealed class ToolExecution
{
    public required ToolCall Call { get; init; }

    public ToolExecutionStatus Status { get; internal set; }
        = ToolExecutionStatus.QUEUED;

    public string? Result { get; internal set; }

    public Exception? Exception { get; internal set; }

    public DateTimeOffset CreatedAt { get; init; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; internal set; }

    public DateTimeOffset? CompletedAt { get; internal set; }

    internal Task<string>? ExecutionTask { get; set; }

    public Task<string> Task =>
        ExecutionTask
        ?? throw new InvalidOperationException(
            "Tool execution has not been scheduled yet.");
}
