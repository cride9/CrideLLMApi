using CrideLLMApi.Api.Tools;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrideLLMApi.Api.Endpoints;

public class ChatCompletion
{
    /// <summary>
    /// Event triggered when a tool call is executed. The event provides the executed ToolCall and the result of the execution as a string.
    /// </summary>
    public event Action<ToolCall, string>? ToolCallExecuted;

    /// <summary>
    /// A list of API functions that can be called during the conversation. These functions are added using the AddFunction method and are used to define the available tools for the LLM to call.
    /// </summary>
    private readonly ToolRegistry _tools = new();

    /// <summary>
    /// The ToolScheduler instance used to manage the scheduling and execution of tool calls. This is initialized in the constructor and is responsible for executing the registered tools when they are called during the conversation.
    /// </summary>
    private readonly ToolScheduler _toolScheduler;

    /// <summary>
    /// The ChatCompletionRequestBuilder instance used to build the request body for the LLM provider's API. This is initialized in the constructor and is used to generate the request payload based on the current conversation context and available tools.
    /// </summary>
    private readonly ChatCompletionRequestBuilder _requestBuilder;

    /// <summary>
    /// The ChatCompletionTransport instance used to send HTTP requests to the LLM provider's API. This is initialized in the constructor and is responsible for handling the communication with the API, including sending requests and receiving responses.
    /// </summary>
    private readonly ChatCompletionTransport _transport;

    /// <summary>
    /// The ContextManager instance used to manage the conversation context. This is optional and can be set using the AddContextManager method.
    /// </summary>
    private ContextManager? _contextManager;

    /// <summary>
    /// A dictionary to accumulate tool calls during the streaming of responses. The key is the index of the tool call, and the value is the ToolCall object.
    /// </summary>
    private Dictionary<int, ToolCall> _toolCalls = new();

    /// <summary>
    /// The last finish reason received from the API response. This is used to determine if further tool calls need to be executed. It is updated during the streaming of responses.
    /// </summary>
    private string? _lastFinishReason;

    public ChatCompletion(ProviderInfo lLMInfo, HttpClient httpClient)
    {
        _requestBuilder =
       new ChatCompletionRequestBuilder(
           lLMInfo,
           _tools,
           () => _contextManager);

        _transport =
            new ChatCompletionTransport(httpClient);


        _toolScheduler = new ToolScheduler(_tools);

        _toolScheduler.ToolCompleted += execution =>
        {
            ToolCallExecuted?.Invoke(
                execution.Call,
                execution.Result ?? string.Empty);
        };

        _toolScheduler.ToolFailed += execution =>
        {
            ToolCallExecuted?.Invoke(
                execution.Call,
                $"Error executing tool " +
                $"'{execution.Call.Function?.Name}': " +
                $"{execution.Exception?.Message}");
        };
    }

    /// <summary>
    /// Gets the list of function calls that have been added to the ChatCompletion instance. This allows external code to retrieve the available API functions that can be called during the conversation.
    /// </summary>
    /// <returns>List of the current functions for this instance</returns>
    public IEnumerable<FunctionCallObject> GetFunctionCalls() =>
        _tools.Definitions;

    /// <summary>
    /// Asynchronously gets the response from the LLM provider for a given message. This method streams the response in chunks and yields each chunk as it is received. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <param name="message">The message for which to get a response.</param>
    /// <returns>An async enumerable of the response chunks.</returns>
    public async IAsyncEnumerable<Delta> GetResponseAsync(string message, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    public async IAsyncEnumerable<ChatCompletionChunk> GetRawResponseAsync(string message, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = _requestBuilder.CreateWithMessage(REQUEST_ROLE.USER, message);

        while (true)
        {
            await foreach (var chunk in StreamChunksAsync(requestBody, cancellationToken))
                yield return chunk;

            if (_lastFinishReason != "tool_calls" || _toolCalls.Count == 0)
                yield break;

            await ExecuteToolCallsAsync(_toolCalls.Values.OrderBy(t => t.Index).ToList());

            requestBody = _requestBuilder.Create();
        }
    }

    /// <summary>
    /// Adds a function to the ChatCompletion instance, allowing it to be called during the conversation. Optionally, a handler can be provided to execute the function when it is called. The function is added to the list of available API functions, and if a handler is provided, it is stored in the tool handlers dictionary for execution.
    /// </summary>
    /// <param name="function">The function to add.</param>
    /// <param name="handler">The handler to execute the function.</param>
    public void AddFunction(
        ApiFunction function,
        ToolExecutor? handler = null)
    {
        _tools.Register(function, handler);
    }

    /// <summary>
    /// Adds a function to the ChatCompletion instance, allowing it to be called during the conversation. A handler is provided to execute the function when it is called. The function is added to the list of available API functions, and the handler is stored in the tool handlers dictionary for execution. The handler takes a generic argument type TArgs, which is deserialized from the JSON arguments provided by the tool call.
    /// </summary>
    /// <typeparam name="TArgs">The type of the arguments for the function.</typeparam>
    /// <param name="function">The function to add.</param>
    /// <param name="handler">The handler to execute the function.</param>
    /// <exception cref="JsonException"></exception>
    public void AddFunction<TArgs>(
        ApiFunction function,
        Func<TArgs, Task<string>> handler)
    {
        _tools.Register(function, handler);
    }

    /// <summary>
    /// Adds a ContextManager instance to the ChatCompletion instance, allowing it to manage the conversation context. The ContextManager is used to store and retrieve messages during the conversation, enabling the LLM to maintain context across multiple interactions. This method sets the internal _contextManager field to the provided ContextManager instance.
    /// </summary>
    /// <param name="ctxManager">The ContextManager instance to add.</param>
    public void AddContextManager(ContextManager ctxManager)
    {
        _contextManager = ctxManager;
    }

    /// <summary>
    /// Asynchronously streams the chat completion response from the LLM provider for a given request body. This method yields each chunk of the response as it is received, allowing for real-time processing of the response. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <param name="requestBody">The request body for the chat completion.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    private async IAsyncEnumerable<ChatCompletionChunk> StreamChunksAsync(object requestBody, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _toolCalls.Clear();
        _lastFinishReason = null;

        using var response =
            await _transport.SendAsync(
                requestBody,
                cancellationToken);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
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

    /// <summary>
    /// Extracts the data portion from a Server-Sent Events (SSE) line. This method checks if the line starts with "data:" and, if so, returns the trimmed content after "data:". If the line is null, empty, or does not start with "data:", it returns null. This is used to parse SSE responses from the LLM provider's API.
    /// </summary>
    /// <param name="line">The SSE line to extract data from.</param>
    /// <returns>The extracted data or null if the line is not valid.</returns>
    private static string? ExtractSseData(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
            return null;

        return line["data:".Length..].Trim();
    }

    /// <summary>
    /// Accumulates tool calls by merging them based on their index. If a tool call with the same index already exists, it merges the function name and arguments. This method ensures that multiple tool calls with the same index are combined into a single tool call, allowing for efficient execution of tools during the conversation.
    /// </summary>
    /// <param name="tool">The tool call to accumulate.</param>
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

    /// <summary>
    /// Asynchronously executes a list of tool calls. This method schedules the execution of each tool call using the ToolScheduler, waits for their completion, and updates the conversation context with the results. It handles different execution statuses (completed, failed, cancelled) and adds appropriate messages to the context manager. After execution, it clears completed tool executions from the scheduler.
    /// </summary>
    /// <param name="toolCalls">The list of tool calls to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteToolCallsAsync(List<ToolCall> toolCalls, CancellationToken cancellationToken = default)
    {
        _contextManager?.Add(new ChatMessage
        {
            Role = REQUEST_ROLE.ASSISTANT
                .ToString()
                .ToLower(),

            Content = null,
            ToolCalls = toolCalls
        });

        var executions = toolCalls
            .Select(tool => _toolScheduler.Schedule(tool, cancellationToken))
            .ToArray();

        try
        {
            await Task.WhenAll(
                executions.Select(x => x.Task));
        }
        catch
        {
            // Individual failures are handled below
            // from each ToolExecution.
        }

        foreach (var execution in executions)
        {
            var result = execution.Status switch
            {
                ToolExecutionStatus.COMPLETED =>
                    execution.Result ?? string.Empty,

                ToolExecutionStatus.FAILED =>
                    $"Error executing tool " +
                    $"'{execution.Call.Function?.Name}': " +
                    $"{execution.Exception?.Message}",

                ToolExecutionStatus.CANCELLED =>
                    $"Tool '{execution.Call.Function?.Name}' was cancelled.",

                _ =>
                    $"Tool '{execution.Call.Function?.Name}' " +
                    $"did not complete."
            };

            _contextManager?.Add(new ChatMessage
            {
                Role = REQUEST_ROLE.TOOL
                    .ToString()
                    .ToLower(),

                ToolCallId = execution.Call.Id,
                Content = result
            });
        }

        _toolScheduler.ClearCompleted();
    }

    /// <summary>
    /// Asynchronously streams the chat completion response from the LLM provider for a given request body. This method yields each chunk of the response as it is received, allowing for real-time processing of the response. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <param name="requestBody">The request body for the chat completion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    public async IAsyncEnumerable<ChatCompletionChunk> StreamCompletionAsync(object requestBody, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (
            var chunk in StreamChunksAsync(requestBody, cancellationToken)
                .WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Asynchronously streams the chat completion response from the LLM provider for a given ChatCompletionRequest. This method yields each chunk of the response as it is received, allowing for real-time processing of the response. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    public async IAsyncEnumerable<ChatCompletionChunk> StreamCompletionAsync(ChatCompletionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (
            var chunk in StreamChunksAsync(request, cancellationToken)
                .WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Creates a new ChatCompletionRequest using the provided messages. This method utilizes the internal ChatCompletionRequestBuilder to generate the request payload based on the current conversation context and available tools. If no messages are provided, it will create a request with an empty message list.
    /// </summary>
    /// <param name="messages">The messages to include in the request.</param>
    /// <returns>The created ChatCompletionRequest.</returns>
    public ChatCompletionRequest CreateRequest(
        IEnumerable<ChatMessage>? messages = null)
    {
        return _requestBuilder.Create(messages);
    }
}
