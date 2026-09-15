using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Tools;

/// <summary>
/// Represents the state of a single tool call managed by the <see cref="ToolScheduler"/>.
/// <para>
/// The scheduler never throws to the caller for an individual tool failure: the failure is
/// stored on the execution so the caller can inspect <see cref="Status"/>,
/// <see cref="Exception"/> and <see cref="Task"/> after the batch completed.
/// </para>
/// </summary>
public sealed class ToolExecution
{
    /// <summary>
    /// Gets the tool call this execution belongs to.
    /// </summary>
    public required ToolCall Call { get; init; }

    /// <summary>
    /// Gets the current status of the execution.
    /// </summary>
    public ToolExecutionStatus Status { get; internal set; }
        = ToolExecutionStatus.QUEUED;

    /// <summary>
    /// Gets the result produced by the tool handler when the execution completed successfully.
    /// </summary>
    public string? Result { get; internal set; }

    /// <summary>
    /// Gets the exception that caused the execution to fail, when it failed.
    /// </summary>
    public Exception? Exception { get; internal set; }

    /// <summary>
    /// Gets the moment the execution was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
        = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the moment the execution started running.
    /// </summary>
    public DateTimeOffset? StartedAt { get; internal set; }

    /// <summary>
    /// Gets the moment the execution reached a terminal status.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; internal set; }

    internal Task<string>? ExecutionTask { get; set; }

    /// <summary>
    /// Gets the task that represents the running execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the execution was not scheduled through <see cref="ToolScheduler.Schedule"/>
    /// yet, or when it could not be scheduled at all. Inspect <see cref="Status"/> first to
    /// distinguish a failed scheduling from a running execution.
    /// </exception>
    public Task<string> Task =>
        ExecutionTask
        ?? throw new InvalidOperationException(
            "Tool execution has not been scheduled yet.");
}
