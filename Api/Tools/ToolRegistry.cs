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

    /// <summary>
    /// Registers a tool with the specified function definition and an optional executor handler. The function definition is converted to a FunctionCallObject, which is stored along with the executor in the registry. If no executor is provided, the tool will be registered without an execution logic.
    /// </summary>
    /// <param name="function">The API function to register.</param>
    /// <param name="handler">The executor handler for the tool.</param>
    public void Register(ApiFunction function, ToolExecutor? handler = null)
    {
        ArgumentNullException.ThrowIfNull(function);

        var definition = function.AsFunctionCall();

        _tools[function.Name] = new RegisteredTool(
            definition,
            handler
        );
    }

    /// <summary>
    /// Registers a tool with the specified function definition and a strongly-typed handler. The handler is a function that takes an argument of type TArgs and returns a Task that resolves to a string result. The arguments are deserialized from JSON before being passed to the handler. If deserialization fails, a JsonException is thrown.
    /// </summary>
    /// <typeparam name="TArgs">The type of the arguments for the tool.</typeparam>
    /// <param name="function">The API function to register.</param>
    /// <param name="handler">The strongly-typed handler for the tool.</param>
    /// <exception cref="JsonException"></exception>
    public void Register<TArgs>(ApiFunction function, Func<TArgs, Task<string>> handler)
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

    /// <summary>
    /// Checks if a tool with the specified name is registered in the registry.
    /// </summary>
    /// <param name="name">The name of the tool to check.</param>
    /// <returns>true if the tool is registered; otherwise, false.</returns>
    public bool Contains(string name)
    {
        return _tools.ContainsKey(name);
    }
    
    /// <summary>
    /// Attempts to get a tool with the specified name from the registry.
    /// </summary>
    /// <param name="name">The name of the tool to get.</param>
    /// <param name="tool">The tool if found; otherwise, null.</param>
    /// <returns>true if the tool is found; otherwise, false.</returns>
    public bool TryGet(string name, out RegisteredTool? tool)
    {
        return _tools.TryGetValue(name, out tool);
    }

    /// <summary>
    /// Removes a tool with the specified name from the registry.
    /// </summary>
    /// <param name="name">The name of the tool to remove.</param>
    /// <returns>true if the tool is removed; otherwise, false.</returns>
    public bool Remove(string name)
    {
        return _tools.Remove(name);
    }

    /// <summary>
    /// Clears all registered tools from the registry.
    /// </summary>
    public void Clear()
    {
        _tools.Clear();
    }
}
