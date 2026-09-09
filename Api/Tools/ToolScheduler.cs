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

    public ToolExecution Schedule(
        ToolCall call,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);

        var execution = new ToolExecution
        {
            Call = call
        };

        var key = GetExecutionKey(call);

        if (!_executions.TryAdd(key, execution))
        {
            throw new InvalidOperationException(
                $"Tool call '{key}' has already been scheduled.");
        }

        ToolScheduled?.Invoke(execution);

        execution.ExecutionTask =
            ExecuteAsync(execution, cancellationToken);

        return execution;
    }

    private async Task<string> ExecuteAsync(
        ToolExecution execution,
        CancellationToken cancellationToken)
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

    public bool TryGet(
        string callId,
        out ToolExecution? execution)
    {
        return _executions.TryGetValue(
            callId,
            out execution);
    }

    public IEnumerable<ToolExecution> GetPending()
    {
        return _executions.Values.Where(x =>
            x.Status is ToolExecutionStatus.QUEUED
            or ToolExecutionStatus.RUNNING);
    }

    public IEnumerable<ToolExecution> GetCompleted()
    {
        return _executions.Values.Where(x =>
            x.Status == ToolExecutionStatus.COMPLETED);
    }

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

    private static string GetExecutionKey(ToolCall call)
    {
        if (!string.IsNullOrWhiteSpace(call.Id))
            return call.Id;

        return $"tool-{call.Index}-{Guid.NewGuid():N}";
    }
}
