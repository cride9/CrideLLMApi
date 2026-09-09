using CrideLLMApi.DTO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace CrideLLMApi.Api.Tools;

/// <summary>
/// Delegate representing a tool executor function. This delegate takes a JSON string of arguments and returns a Task that resolves to a string result. It is used to define the execution logic for tools that can be called during the conversation.
/// </summary>
/// <param name="argumentsJson">The JSON string of arguments for the tool.</param>
/// <returns>A Task that resolves to the result of the tool execution.</returns>
public delegate Task<string> ToolExecutor(string argumentsJson);

public sealed class ToolRegistry
{
    private readonly Dictionary<string, RegisteredTool> _tools = new();

    public IEnumerable<FunctionCallObject> Definitions =>
        _tools.Values.Select(x => x.Definition);

    public IEnumerable<RegisteredTool> Tools =>
        _tools.Values;

    public void Register(
        ApiFunction function,
        ToolExecutor? handler = null)
    {
        ArgumentNullException.ThrowIfNull(function);

        var definition = function.AsFunctionCall();

        _tools[function.Name] = new RegisteredTool(
            definition,
            handler
        );
    }

    public void Register<TArgs>(
        ApiFunction function,
        Func<TArgs, Task<string>> handler)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(handler);

        ToolExecutor executor = async argumentsJson =>
        {
            var args = JsonSerializer.Deserialize<TArgs>(argumentsJson)
                ?? throw new JsonException(
                    $"Failed to deserialize arguments for tool '{function.Name}'.");

            return await handler(args);
        };

        Register(function, executor);
    }
    public bool Contains(string name)
    {
        return _tools.ContainsKey(name);
    }
    
    public bool TryGet(
        string name,
        out RegisteredTool? tool)
    {
        return _tools.TryGetValue(name, out tool);
    }

    public bool Remove(string name)
    {
        return _tools.Remove(name);
    }

    public void Clear()
    {
        _tools.Clear();
    }
}
