using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CrideLLMApi.Api.CrideApi;

namespace CrideLLMApi.Api.Endpoints;

public class ChatCompletion
{
    /// <summary>
    /// Event triggered when a tool call is executed. The event provides the executed ToolCall and the result of the execution as a string.
    /// </summary>
    public event Action<ToolCall, string>? ToolCallExecuted;

    /// <summary>
    /// Delegate representing a tool executor function. This delegate takes a JSON string of arguments and returns a Task that resolves to a string result. It is used to define the execution logic for tools that can be called during the conversation.
    /// </summary>
    /// <param name="argumentsJson">The JSON string of arguments for the tool.</param>
    /// <returns>A Task that resolves to the result of the tool execution.</returns>
    public delegate Task<string> ToolExecutor(string argumentsJson);

    /// <summary>
    /// The ContextManager instance used to manage the conversation context. This is optional and can be set using the AddContextManager method.
    /// </summary>
    private ContextManager? _contextManager;

    /// <summary>
    /// A dictionary to accumulate tool calls during the streaming of responses. The key is the index of the tool call, and the value is the ToolCall object.
    /// </summary>
    private Dictionary<int, ToolCall> _toolCalls = new();

    /// <summary>
    /// A list of FunctionCallObject instances representing the API functions that can be called. This list is populated using the AddFunction methods.
    /// </summary>
    private List<FunctionCallObject> _apiFunctions = new();

    /// <summary>
    /// The last finish reason received from the API response. This is used to determine if further tool calls need to be executed. It is updated during the streaming of responses.
    /// </summary>
    private string? _lastFinishReason;

    /// <summary>
    /// The ProviderInfo instance containing information about the LLM provider, such as the endpoint, API key, model name, and other configuration settings.
    /// </summary>
    private ProviderInfo _llmInfo;

    /// <summary>
    /// The HttpClient instance used to send HTTP requests to the LLM provider's API. This is initialized in the constructor and used throughout the class to make API calls.
    /// </summary>
    private HttpClient _httpClient;

    /// <summary>
    /// A dictionary mapping tool names to their corresponding ToolExecutor handlers. This allows for the execution of specific tools when they are called during the streaming of responses.
    /// </summary>
    private readonly Dictionary<string, ToolExecutor> _toolHandlers = new();

    /// <summary>
    /// The JsonSerializerOptions instance used for JSON serialization and deserialization. It is configured to ignore null values when writing JSON, which helps reduce the size of the request payloads sent to the API.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ChatCompletion(ProviderInfo lLMInfo, HttpClient httpClient)
    {
        _llmInfo = lLMInfo;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the list of function calls that have been added to the ChatCompletion instance. This allows external code to retrieve the available API functions that can be called during the conversation.
    /// </summary>
    /// <returns>List of the current functions for this instance</returns>
    public IEnumerable<FunctionCallObject> GetFunctionCalls() =>
        _apiFunctions.AsReadOnly();

    /// <summary>
    /// Asynchronously gets the response from the LLM provider for a given message. This method streams the response in chunks and yields each chunk as it is received. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <param name="message">The message for which to get a response.</param>
    /// <returns>An async enumerable of the response chunks.</returns>
    public async IAsyncEnumerable<Delta> GetResponseAsync(string message)
    {
        var response = GetRawResponseAsync(message);
        await foreach (var item in response)
        {
            if (!string.IsNullOrWhiteSpace(item?.Choices[0]?.Delta?.Content))
                yield return item.Choices[0].Delta!;
        }
    }

    /// <summary>
    /// Asynchronously gets the raw response from the LLM provider for a given message. This method streams the response in chunks and yields each chunk as it is received. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <param name="message">The message for which to get a raw response.</param>
    /// <returns>An async enumerable of the raw response chunks.</returns>
    public async IAsyncEnumerable<ChatCompletionChunk> GetRawResponseAsync(string message)
    {
        var requestBody = GenerateRequestBody(REQUEST_ROLE.USER, message);

        while (true)
        {
            await foreach (var chunk in StreamChunksAsync(requestBody))
                yield return chunk;

            if (_lastFinishReason != "tool_calls" || _toolCalls.Count == 0)
                yield break;

            await ExecuteToolCallsAsync(_toolCalls.Values.OrderBy(t => t.Index).ToList());

            requestBody = BuildRequestBody();
        }
    }

    /// <summary>
    /// Adds a function to the ChatCompletion instance, allowing it to be called during the conversation. Optionally, a handler can be provided to execute the function when it is called. The function is added to the list of available API functions, and if a handler is provided, it is stored in the tool handlers dictionary for execution.
    /// </summary>
    /// <param name="function">The function to add.</param>
    /// <param name="handler">The handler to execute the function.</param>
    public void AddFunction(ApiFunction function, ToolExecutor? handler = null)
    {
        _apiFunctions.Add(function.AsFunctionCall( ));

        if ( handler != null )
            _toolHandlers[ function.Name ] = handler;
    }

    /// <summary>
    /// Adds a function to the ChatCompletion instance, allowing it to be called during the conversation. A handler is provided to execute the function when it is called. The function is added to the list of available API functions, and the handler is stored in the tool handlers dictionary for execution. The handler takes a generic argument type TArgs, which is deserialized from the JSON arguments provided by the tool call.
    /// </summary>
    /// <typeparam name="TArgs">The type of the arguments for the function.</typeparam>
    /// <param name="function">The function to add.</param>
    /// <param name="handler">The handler to execute the function.</param>
    /// <exception cref="JsonException"></exception>
    public void AddFunction<TArgs>(ApiFunction function, Func<TArgs, Task<string>> handler)
    {
        _apiFunctions.Add(function.AsFunctionCall( ));

        _toolHandlers[ function.Name ] = async argumentsJson =>
        {
            var args = JsonSerializer.Deserialize<TArgs>(argumentsJson)
                ?? throw new JsonException(
                    $"Failed to deserialize arguments for tool '{function.Name}'.");

            return await handler(args);
        };
    }

    /// <summary>
    /// Adds a ContextManager instance to the ChatCompletion instance, allowing it to manage the conversation context. The ContextManager is used to store and retrieve messages during the conversation, enabling the LLM to maintain context across multiple interactions. This method sets the internal _contextManager field to the provided ContextManager instance.
    /// </summary>
    /// <param name="ctxManager">The ContextManager instance to add.</param>
    public void AddContextManager(ContextManager ctxManager)
    {
        _contextManager = ctxManager;
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamChunksAsync(object requestBody)
    {
        _toolCalls.Clear();
        _lastFinishReason = null;

        using var response = await ChatCompletionBuilder(requestBody);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
                break;

            var json = ExtractSseData(line);
            if (json == null)
                continue;
            if (json == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(json)!;
            var choice = chunk.Choices[0];

            _lastFinishReason = choice.FinishReason ?? _lastFinishReason;

            foreach (var tool in choice.Delta.ToolCalls ?? [])
                AccumulateToolCall(tool);

            yield return chunk;
        }
    }

    private static string? ExtractSseData(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
            return null;

        return line["data:".Length..].Trim();
    }

    private void AccumulateToolCall(ToolCall tool)
    {
        if (!_toolCalls.TryGetValue(tool.Index, out var existing))
        {
            existing = tool with { Function = new ToolFunction() };
            _toolCalls[tool.Index] = existing;
        }

        if (tool.Function != null)
        {
            existing.Function!.Name ??= tool.Function.Name;
            existing.Function!.Arguments += tool.Function.Arguments;
        }
    }

    private async Task ExecuteToolCallsAsync(List<ToolCall> toolCalls)
    {
        _contextManager?.Add(new ChatMessage
        {
            Role = REQUEST_ROLE.ASSISTANT.ToString().ToLower(),
            Content = null,
            ToolCalls = toolCalls
        });

        foreach (var tool in toolCalls)
        {
            var result = await RunToolAsync(tool);

            _contextManager?.Add(new ChatMessage
            {
                Role = REQUEST_ROLE.TOOL.ToString().ToLower(),
                ToolCallId = tool.Id,
                Content = result
            });
        }
    }

    private async Task<string> RunToolAsync(ToolCall tool)
    {
        var name = tool.Function?.Name;
        string result;

        if (name == null || !_toolHandlers.TryGetValue(name, out var handler))
        {
            result = $"No handler registered for tool '{name}'.";
        }
        else
        {
            try
            {
                result = await handler(tool.Function!.Arguments ?? "{}");
            }
            catch (Exception ex)
            {
                result = $"Error executing tool '{name}': {ex.Message}";
            }
        }
        ToolCallExecuted?.Invoke(tool, result);
        return result;
    }

    private object BuildRequestBody() => new
    {
        model = _llmInfo.ModelName,
        messages = _contextManager?.GetMessages() ?? new List<ChatMessage>(),
        tools = _apiFunctions,
        stream = _llmInfo.Streaming,
        reasoning_effort = _llmInfo.ReasoningEffort.ToString().ToLower(),
        tool_choice = _llmInfo.ToolChoice.ToString().ToLower()
    };

    private object GenerateRequestBody(REQUEST_ROLE requestType, string content)
    {
        _contextManager?.Add(new ChatMessage { Role = requestType.ToString().ToLower(), Content = content });
        return BuildRequestBody();
    }

    private async Task<HttpResponseMessage> ChatCompletionBuilder(object requestBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");

        request.Content = JsonContent.Create(requestBody, options: _jsonOptions);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

}
