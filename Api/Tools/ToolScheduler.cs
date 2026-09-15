using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Collections.Concurrent;

namespace CrideLLMApi.Api.Tools;

public sealed class ToolScheduler
{
    private readonly ToolRegistry _registry;

    private readonly ConcurrentDictionary<string, ToolExecution>
        _executions = new();

    public event Action<ToolExecution>? ToolScheduled;
    public event Action<ToolExecution>? ToolStarted;
    public event Action<ToolExecution>? ToolCompleted;
    public event Action<ToolExecution>? ToolFailed;

    public ToolScheduler(ToolRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyCollection<ToolExecution> Executions =>
        _executions.Values.ToArray();

    /// <summary>
    /// Schedules a tool call for execution.
    /// </summary>
    /// <param name="call">The tool call to schedule.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scheduled tool execution.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ToolExecution Schedule(ToolCall call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);

        var execution = new ToolExecution
        {
            Call = call
        };

        var key = GetExecutionKey(call);

        if (!_executions.TryAdd(key, execution))
        {
            throw new CrideApiException(
                message:
                    $"Tool call '{key}' has already been scheduled, so it cannot be scheduled again.",
                component: "ToolScheduler",
                operation: "Schedule a tool call for execution",
                hint:
                    "Use a distinct tool call id for every call, and clear the completed executions with ClearCompleted before scheduling the same call again.");
        }

        ToolScheduled?.Invoke(execution);

        execution.ExecutionTask =
            ExecuteAsync(execution, cancellationToken);

        return execution;
    }

    /// <summary>
    /// Executes the specified tool call asynchronously and updates the associated
    /// <see cref="ToolExecution"/> state throughout the execution lifecycle.
    /// </summary>
    /// <param name="execution">
    /// The tool execution instance containing the tool call and its current execution state.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the execution before or during processing.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous tool execution.
    /// The task result contains the string returned by the registered tool handler.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tool call does not contain a valid function name, or when no executable
    /// handler is registered for the requested tool. Both failures are stored on the returned
    /// <see cref="ToolExecution"/> as well, so a failing tool never aborts the whole batch.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled through <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task<string> ExecuteAsync(ToolExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        execution.Status = ToolExecutionStatus.RUNNING;
        execution.StartedAt = DateTimeOffset.UtcNow;

        ToolStarted?.Invoke(execution);

        try
        {
            var call = execution.Call;
            var name = call.Function?.Name;

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    "Tool call does not contain a function name.");
            }

            if (!_registry.TryGet(name, out var registeredTool) ||
                registeredTool?.Executor is null)
            {
                throw new InvalidOperationException(
                    $"No handler registered for tool '{name}'.");
            }

            var arguments =
                call.Function?.Arguments ?? "{}";

            var result = await registeredTool.Executor(arguments);

            execution.Result = result;
            execution.Status = ToolExecutionStatus.COMPLETED;
            execution.CompletedAt = DateTimeOffset.UtcNow;

            ToolCompleted?.Invoke(execution);

            return result;
        }
        catch (OperationCanceledException)
        {
            execution.Status = ToolExecutionStatus.CANCELLED;
            execution.CompletedAt = DateTimeOffset.UtcNow;

            throw;
        }
        catch (Exception ex)
        {
            execution.Exception = ex;
            execution.Status = ToolExecutionStatus.FAILED;
            execution.CompletedAt = DateTimeOffset.UtcNow;

            ToolFailed?.Invoke(execution);

            throw;
        }
    }

    /// <summary>
    /// Attempts to retrieve the <see cref="ToolExecution"/> associated with the specified call ID.
    /// </summary>
    /// <param name="callId">The ID of the tool call to retrieve.</param>
    /// <param name="execution">The tool execution instance if found; otherwise, null.</param>
    /// <returns>true if the tool execution is found; otherwise, false.</returns>
    public bool TryGet(string callId, out ToolExecution? execution)
    {
        return _executions.TryGetValue(
            callId,
            out execution);
    }

    /// <summary>
    /// Retrieves all tool executions that are currently pending, which includes those that are queued or running.
    /// </summary>
    /// <returns>An enumerable of pending tool executions.</returns>
    public IEnumerable<ToolExecution> GetPending()
    {
        return _executions.Values.Where(x =>
            x.Status is ToolExecutionStatus.QUEUED
            or ToolExecutionStatus.RUNNING);
    }

    /// <summary>
    /// Retrieves all tool executions that reached a terminal status, which includes those that
    /// completed, failed, or were cancelled.
    /// </summary>
    /// <returns>The tool executions that are no longer running.</returns>
    public IEnumerable<ToolExecution> GetCompleted()
    {
        return _executions.Values.Where(x =>
            x.Status is ToolExecutionStatus.COMPLETED
            or ToolExecutionStatus.FAILED
            or ToolExecutionStatus.CANCELLED);
    }

    /// <summary>
    /// Flushes all pending tool executions by awaiting their completion. Individual failures are
    /// not thrown: inspect <see cref="ToolExecution.Status"/> and
    /// <see cref="ToolExecution.Exception"/> on the returned executions.
    /// </summary>
    /// <returns>An array of the flushed tool executions.</returns>
    public async Task<ToolExecution[]> FlushAsync()
    {
        var pending = GetPending().ToArray();

        if (pending.Length == 0)
            return [];

        try
        {
            await Task.WhenAll(
                pending.Select(x => x.Task));
        }
        catch
        {
            // Intentionally ignored here.
            // Individual ToolExecution objects contain failure state.
        }

        return pending;
    }

    /// <summary>
    /// Clears all completed tool executions from the scheduler, including those that have completed successfully, failed, or were cancelled.
    /// </summary>
    public void ClearCompleted()
    {
        foreach (var pair in _executions)
        {
            if (pair.Value.Status is
                ToolExecutionStatus.COMPLETED or
                ToolExecutionStatus.FAILED or
                ToolExecutionStatus.CANCELLED)
            {
                _executions.TryRemove(
                    pair.Key,
                    out _);
            }
        }
    }

    /// <summary>
    /// Generates a unique execution key for the given <see cref="ToolCall"/>. If the call has an explicit ID, that ID is used; otherwise, a new GUID-based key is generated, so calls without an ID can never collide with each other.
    /// </summary>
    /// <param name="call">The tool call for which to generate an execution key.</param>
    /// <returns>The unique execution key.</returns>
    private static string GetExecutionKey(ToolCall call)
    {
        if (!string.IsNullOrWhiteSpace(call.Id))
            return call.Id;

        return $"tool-{call.Index}-{Guid.NewGuid():N}";
    }
}
