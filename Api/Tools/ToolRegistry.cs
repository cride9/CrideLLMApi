using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Text.Json;

namespace CrideLLMApi.Api.Tools;

/// <summary>
/// Delegate representing a tool executor function. This delegate takes a JSON string of arguments and returns a Task that resolves to a string result. It is used to define the execution logic for tools that can be called during the conversation.
/// </summary>
/// <param name="argumentsJson">The JSON string of arguments for the tool.</param>
/// <returns>A Task that resolves to the result of the tool execution.</returns>
public delegate Task<string> ToolExecutor(string argumentsJson);

/// <summary>
/// Registry of the tool definitions and their executors for a chat conversation. A tool is
/// identified by its function name, so the name must be unique.
/// </summary>
public sealed class ToolRegistry
{
    private const string ComponentName = "ToolRegistry";

    private readonly Dictionary<string, RegisteredTool> _tools = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the tool definitions that are exposed to the model.
    /// </summary>
    public IEnumerable<FunctionCallObject> Definitions =>
        _tools.Values.Select(x => x.Definition);

    /// <summary>
    /// Gets the registered tools, including their optional executors.
    /// </summary>
    public IEnumerable<RegisteredTool> Tools =>
        _tools.Values;

    private static readonly JsonSerializerOptions ToolJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Registers a tool with the specified function definition and an optional executor handler. The function definition is converted to a FunctionCallObject, which is stored along with the executor in the registry. If no executor is provided, the tool will be registered without an execution logic.
    /// </summary>
    /// <param name="function">The API function to register.</param>
    /// <param name="handler">The executor handler for the tool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the function has no name, or when a tool with the same name is already
    /// registered. The model calls tools by name, so silently replacing a tool would change the
    /// behavior of an existing conversation.
    /// </exception>
    public void Register(ApiFunction function, ToolExecutor? handler = null)
    {
        ArgumentNullException.ThrowIfNull(function);

        var name = function.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "The tool definition must have a non-empty function name.",
                nameof(function));
        }

        if (_tools.ContainsKey(name))
        {
            throw new ArgumentException(
                $"A tool named '{name}' is already registered. Tool names must be unique; " +
                "remove or rename the existing tool before registering another one with the same name.",
                nameof(function));
        }

        var definition = function.AsFunctionCall();

        _tools[name] = new RegisteredTool(
            definition,
            handler
        );
    }

    /// <summary>
    /// Registers a tool with the specified function definition and a strongly-typed handler. The handler is a function that takes an argument of type TArgs and returns a Task that resolves to a string result. The arguments are deserialized from JSON before being passed to the handler. A missing, empty or whitespace argument string is treated as an empty object, because providers commonly report tools without parameters that way.
    /// </summary>
    /// <typeparam name="TArgs">The type of the arguments for the tool.</typeparam>
    /// <param name="function">The API function to register.</param>
    /// <param name="handler">The strongly-typed handler for the tool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> or <paramref name="handler"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the function has no name, or when a tool with the same name is already
    /// registered.
    /// </exception>
    /// <exception cref="CrideApiException">
    /// Thrown at execution time when the arguments produced by the model cannot be deserialized
    /// into <typeparamref name="TArgs"/>, with the offending JSON payload attached.
    /// </exception>
    public void Register<TArgs>(ApiFunction function, Func<TArgs, Task<string>> handler)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(handler);

        var name = function.Name;

        ToolExecutor executor = async argumentsJson =>
        {
            // Providers frequently report a tool without parameters with an empty argument
            // string, which is not valid JSON. Normalizing it keeps the call working instead of
            // failing with a schema error the model cannot act on.
            var json = string.IsNullOrWhiteSpace(argumentsJson)
                ? "{}"
                : argumentsJson;

            TArgs? args;

            try
            {
                args = JsonSerializer.Deserialize<TArgs>(
                    json,
                    ToolJsonOptions);
            }
            catch (JsonException exception)
            {
                throw new CrideApiException(
                    message:
                        $"The arguments produced by the model for tool '{name}' could not be deserialized into {typeof(TArgs).Name}.",
                    component: ComponentName,
                    operation: $"Execute tool '{name}'",
                    innerException: exception,
                    responseBody: json,
                    hint:
                        $"Check that the parameter schema declared in ApiFunction.Properties matches {typeof(TArgs).Name}.");
            }

            if (args is null)
            {
                throw new CrideApiException(
                    message:
                        $"The arguments produced by the model for tool '{name}' deserialized to null.",
                    component: ComponentName,
                    operation: $"Execute tool '{name}'",
                    responseBody: json,
                    hint:
                        $"Check the parameters the model sent above against {typeof(TArgs).Name}.");
            }

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
        return name is not null && _tools.ContainsKey(name);
    }
    
    /// <summary>
    /// Attempts to get a tool with the specified name from the registry.
    /// </summary>
    /// <param name="name">The name of the tool to get.</param>
    /// <param name="tool">The tool if found; otherwise, null.</param>
    /// <returns>true if the tool is found; otherwise, false.</returns>
    public bool TryGet(string name, out RegisteredTool? tool)
    {
        if (name is null)
        {
            tool = null;
            return false;
        }

        return _tools.TryGetValue(name, out tool);
    }

    /// <summary>
    /// Removes a tool with the specified name from the registry.
    /// </summary>
    /// <param name="name">The name of the tool to remove.</param>
    /// <returns>true if the tool is removed; otherwise, false.</returns>
    public bool Remove(string name)
    {
        return name is not null && _tools.Remove(name);
    }

    /// <summary>
    /// Clears all registered tools from the registry.
    /// </summary>
    public void Clear()
    {
        _tools.Clear();
    }
}
