using CrideLLMApi.Api.Tools;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CrideLLMApi.Api.Endpoints;

public class ChatCompletion
{
    private const string ComponentName = "ChatCompletion";

    /// <summary>
    /// The maximum number of automatic tool-call rounds executed by
    /// <see cref="GetRawResponseAsync(string, CancellationToken)"/> before the loop is stopped
    /// with an error instead of running forever.
    /// </summary>
    private const int MaxToolLoopIterations = 25;

    /// <summary>
    /// The maximum number of characters buffered from a response that does not look like a
    /// server-sent event stream. The buffer is used to build an actionable error message.
    /// </summary>
    private const int MaxNonSsePreviewLength = 2048;

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
    /// The state of a single streamed round. The tool call fragments are accumulated per
    /// operation, so two concurrent calls on the same instance never share - and never corrupt -
    /// each other's stream state.
    /// </summary>
    private sealed class StreamState
    {
        /// <summary>
        /// Gets the accumulated tool call fragments of the round, keyed by the tool call index.
        /// </summary>
        public Dictionary<int, ToolCall> ToolCalls { get; } = new();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCompletion"/> endpoint.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for provider communication.</param>
    /// <param name="lLMInfo">The provider configuration.</param>
    /// <exception cref="CrideApiException">
    /// Thrown when the chat configuration is not supported, for example when
    /// <see cref="ChatProviderOptions.Streaming"/> is <see langword="false"/>.
    /// </exception>
    public ChatCompletion(HttpClient httpClient, ProviderInfo lLMInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(lLMInfo);

        if (!lLMInfo.Chat.Streaming)
        {
            throw new CrideApiException(
                message:
                    "ProviderInfo.Chat.Streaming is false, but non-streaming chat completions are not supported yet.",
                component: ComponentName,
                operation: "Validate ProviderInfo.Chat.Streaming",
                hint:
                    "Set ProviderInfo.Chat.Streaming to true. With stream false the provider returns a single JSON object that the streaming pipeline cannot process.");
        }

        _requestBuilder =
       new ChatCompletionRequestBuilder(
           lLMInfo,
           _tools,
           () => _contextManager);

        _transport =
            new ChatCompletionTransport(httpClient, lLMInfo);


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
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of the response chunks.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null, empty or whitespace.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider fails, when the payload cannot be processed, or when the
    /// automatic tool loop cannot continue. See
    /// <see cref="GetRawResponseAsync(string, CancellationToken)"/> for the detailed cases.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation through <paramref name="cancellationToken"/>.
    /// </exception>
    public IAsyncEnumerable<Delta> GetResponseAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return GetResponseCoreAsync(message, cancellationToken);
    }

    /// <summary>
    /// Asynchronously gets the raw response from the LLM provider for a given message. This method streams the response in chunks and yields each chunk as it is received. It also handles tool calls that may be triggered during the conversation, executing them as needed and updating the context accordingly.
    /// </summary>
    /// <remarks>
    /// A round that produced any tool call fragment is executed, regardless of the finish reason
    /// reported by the provider, because several OpenAI-compatible servers answer tool calls with
    /// "stop" or "function_call". Every call keeps its own stream state; the attached
    /// <see cref="ContextManager"/> is shared by the instance.
    /// </remarks>
    /// <param name="message">The message for which to get a raw response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of the raw response chunks.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null, empty or whitespace.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider returns an unsuccessful status code, when the connection fails,
    /// when the response is not a server-sent event stream, when a chunk payload cannot be
    /// deserialized, when the automatic tool loop would repeat forever, or when the loop reaches
    /// <see cref="MaxToolLoopIterations"/> rounds.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation through <paramref name="cancellationToken"/>.
    /// </exception>
    public IAsyncEnumerable<ChatCompletionChunk> GetRawResponseAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return GetRawResponseCoreAsync(message, cancellationToken);
    }

    /// <summary>
    /// Registers a tool definition and optionally associates an executor with it.
    /// </summary>
    /// <param name="function">
    /// The tool definition describing the function name, description, parameters,
    /// and other metadata exposed to the model.
    /// </param>
    /// <param name="handler">
    /// The optional executor invoked when the registered tool is called.
    /// If no handler is provided, only the tool definition is registered.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the tool definition is missing a function name, or when another tool with the
    /// same name is already registered.
    /// </exception>
    public void AddFunction(ApiFunction function, ToolExecutor? handler = null)
    {
        _tools.Register(function, handler);
    }

    /// <summary>
    /// Adds a function to the ChatCompletion instance, allowing it to be called during the conversation. A handler is provided to execute the function when it is called. The function is added to the list of available API functions, and the handler is stored in the tool handlers dictionary for execution. The handler takes a generic argument type TArgs, which is deserialized from the JSON arguments provided by the tool call.
    /// </summary>
    /// <typeparam name="TArgs">The type of the arguments for the function.</typeparam>
    /// <param name="function">The function to add.</param>
    /// <param name="handler">The handler to execute the function.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the tool definition is missing a function name, when the handler is null, or
    /// when another tool with the same name is already registered.
    /// </exception>
    /// <exception cref="CrideApiException">
    /// Thrown at execution time when the tool arguments produced by the model cannot be
    /// deserialized into <typeparamref name="TArgs"/>.
    /// </exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ctxManager"/> is null.</exception>
    public void AddContextManager(ContextManager ctxManager)
    {
        ArgumentNullException.ThrowIfNull(ctxManager);

        _contextManager = ctxManager;
    }

    /// <summary>
    /// Asynchronously streams the chat completion response from the LLM provider for a given request body. This method yields each chunk of the response as it is received, allowing for real-time processing of the response. It does not execute the automatic tool loop.
    /// </summary>
    /// <param name="requestBody">The request body for the chat completion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestBody"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider fails, when the response is not an SSE stream, or when a chunk
    /// payload cannot be deserialized.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation through <paramref name="cancellationToken"/>.
    /// </exception>
    public IAsyncEnumerable<ChatCompletionChunk> StreamCompletionAsync(object requestBody, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        return StreamCompletionCoreAsync(requestBody, cancellationToken);
    }

    /// <summary>
    /// Asynchronously streams the chat completion response from the LLM provider for a given ChatCompletionRequest. This method yields each chunk of the response as it is received, allowing for real-time processing of the response. It does not execute the automatic tool loop.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider fails, when the response is not an SSE stream, or when a chunk
    /// payload cannot be deserialized.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation through <paramref name="cancellationToken"/>.
    /// </exception>
    public IAsyncEnumerable<ChatCompletionChunk> StreamCompletionAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return StreamCompletionCoreAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates a new ChatCompletionRequest using the provided messages. This method utilizes the internal ChatCompletionRequestBuilder to generate the request payload based on the current conversation context and available tools. If no messages are provided, it will create a request with an empty message list.
    /// </summary>
    /// <param name="messages">The messages to include in the request.</param>
    /// <returns>The created ChatCompletionRequest.</returns>
    /// <exception cref="CrideApiException">
    /// Thrown when the configured provider mode requires a model name and
    /// <see cref="ChatProviderOptions.ModelName"/> is not set.
    /// </exception>
    public ChatCompletionRequest CreateRequest(
        IEnumerable<ChatMessage>? messages = null)
    {
        return _requestBuilder.Create(messages);
    }

    private async IAsyncEnumerable<Delta> GetResponseCoreAsync(string message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (
            var chunk in GetRawResponseCoreAsync(message, cancellationToken)
                .WithCancellation(cancellationToken))
        {
            var delta = chunk.Choices?.FirstOrDefault()?.Delta;

            if (!string.IsNullOrWhiteSpace(delta?.Content))
                yield return delta!;
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> GetRawResponseCoreAsync(string message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestBody = _requestBuilder.CreateWithMessage(REQUEST_ROLE.USER, message);

        for (var iteration = 0; ; iteration++)
        {
            // Operation-local state: a concurrent call on the same instance must not be able to
            // overwrite the tool call fragments of this round.
            var state = new StreamState();

            await foreach (
                var chunk in StreamChunksAsync(requestBody, state, cancellationToken)
                    .WithCancellation(cancellationToken))
            {
                yield return chunk;
            }

            // A cancellation observed between two chunks must surface as a cancellation instead
            // of ending the stream silently, otherwise the loop below would still execute the
            // tools and send another request with an already cancelled token.
            cancellationToken.ThrowIfCancellationRequested();

            // Tool calls are executed whenever the round produced any, regardless of the finish
            // reason reported by the provider: several OpenAI-compatible servers answer tool
            // calls with "stop" or "function_call", and dropping them would silently lose the
            // tool call.
            if (state.ToolCalls.Count == 0)
                yield break;

            if (_contextManager is null)
            {
                throw new CrideApiException(
                    message:
                        "The model requested tool calls, but no ContextManager is attached, so the tool results cannot be recorded and the same request would be sent to the provider again indefinitely.",
                    component: ComponentName,
                    operation: "Execute the automatic tool loop",
                    hint:
                        "Call AddContextManager(new ContextManager()) before starting the conversation, or use StreamCompletionAsync when you want a single request without the automatic tool loop.");
            }

            if (iteration >= MaxToolLoopIterations)
            {
                throw new CrideApiException(
                    message:
                        $"The automatic tool loop reached the maximum of {MaxToolLoopIterations} rounds because the model kept requesting tool calls.",
                    component: ComponentName,
                    operation: "Execute the automatic tool loop",
                    hint:
                        "Check whether the tool results contain repeated errors that make the model retry, reduce the number of registered tools, or drive the loop manually with StreamCompletionAsync.");
            }

            await ExecuteToolCallsAsync(
                state.ToolCalls.Values
                    .OrderBy(toolCall => toolCall.Index)
                    .ToList(),
                cancellationToken);

            requestBody = _requestBuilder.Create();
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamCompletionCoreAsync(object requestBody, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var state = new StreamState();

        await foreach (
            var chunk in StreamChunksAsync(requestBody, state, cancellationToken)
                .WithCancellation(cancellationToken))
        {
            yield return chunk;
        }

        // The one-shot path does not run the tool loop, but it must still report cancellation
        // instead of ending the stream silently.
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Asynchronously streams the chat completion response from the LLM provider for a given request body. This method yields each chunk of the response as it is received, allowing for real-time processing of the response.
    /// </summary>
    /// <param name="requestBody">The request body for the chat completion.</param>
    /// <param name="state">The stream state the round accumulates its tool call fragments into.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    private async IAsyncEnumerable<ChatCompletionChunk> StreamChunksAsync(object requestBody, StreamState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await _transport.SendAsync(requestBody, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var sawSseData = false;
        var nonSsePreview = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            var json = ExtractSseData(line);
            if (json == null)
            {
                // Everything before the first SSE data line is buffered so a non-streaming or
                // otherwise unexpected payload can be reported with its content instead of
                // producing an empty stream.
                if (!sawSseData &&
                    !string.IsNullOrWhiteSpace(line) &&
                    nonSsePreview.Length < MaxNonSsePreviewLength)
                {
                    nonSsePreview.AppendLine(line);
                }

                continue;
            }

            if (json.Length == 0)
            {
                // Keep-alive or empty data event: nothing to process, the stream is still valid.
                sawSseData = true;
                continue;
            }

            if (json == "[DONE]")
                break;

            sawSseData = true;
            var chunk = DeserializeChunk(json);

            // Chunks without choices exist in practice, for example final usage-only chunks, and
            // must not break the stream.
            var choice = chunk.Choices?.FirstOrDefault();

            if (choice != null)
            {
                foreach (var tool in choice.Delta?.ToolCalls ?? [])
                    AccumulateToolCall(tool, state);
            }

            yield return chunk;
        }

        // The read loop also ends when the caller cancels, and a cancelled stream must not look
        // like a completed one.
        cancellationToken.ThrowIfCancellationRequested();

        if (!sawSseData && nonSsePreview.Length > 0)
        {
            throw new CrideApiException(
                message:
                    "The provider did not return a server-sent event stream for the chat completion request. This happens when the request is sent with \"stream\": false, or when the server does not implement streaming for this route.",
                component: ComponentName,
                operation: _transport.Operation,
                responseBody: nonSsePreview.ToString(),
                hint:
                    "Keep ProviderInfo.Chat.Streaming set to true. Non-streaming chat completions are not supported yet.");
        }
    }

    /// <summary>
    /// Deserializes a single SSE payload into a <see cref="ChatCompletionChunk"/>, reporting the
    /// offending payload when the provider sends something that cannot be processed.
    /// </summary>
    /// <param name="json">The SSE data payload.</param>
    /// <returns>The deserialized chunk.</returns>
    /// <exception cref="CrideApiException">Thrown when the payload is not a valid chunk.</exception>
    private ChatCompletionChunk DeserializeChunk(string json)
    {
        ChatCompletionChunk? chunk;

        try
        {
            chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(
                json,
                ChunkJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new CrideApiException(
                message:
                    "A chat completion chunk sent by the provider could not be deserialized.",
                component: ComponentName,
                operation: _transport.Operation,
                innerException: exception,
                responseBody: json,
                hint:
                    "Compare the response above with the OpenAI chat completion chunk format. The provider may be returning a different payload shape or an error object inside the stream.");
        }

        if (chunk is null)
        {
            throw new CrideApiException(
                message:
                    "The provider sent a chat completion chunk with a null payload.",
                component: ComponentName,
                operation: _transport.Operation,
                responseBody: json,
                hint:
                    "Check the provider implementation: each SSE data event is expected to contain an OpenAI-compatible chat completion chunk.");
        }

        return chunk;
    }

    /// <summary>
    /// Extracts the data portion from a Server-Sent Events (SSE) line. This method checks if the line starts with "data:" and, if so, returns the trimmed content after "data:". If the line is null, empty, or does not start with "data:", it returns null. This is used to parse SSE responses from the LLM provider's API.
    /// </summary>
    /// <param name="line">The SSE line to extract data from.</param>
    /// <returns>The extracted data or null if the line is not valid.</returns>
    private static string? ExtractSseData(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            return null;

        return line["data:".Length..].Trim();
    }

    /// <summary>
    /// Accumulates tool calls by merging them based on their index. If a tool call with the same index already exists, it merges the function name and arguments. This method ensures that multiple tool calls with the same index are combined into a single tool call, allowing for efficient execution of tools during the conversation.
    /// </summary>
    /// <param name="tool">The tool call to accumulate.</param>
    /// <param name="state">The state of the round the tool call belongs to.</param>
    private static void AccumulateToolCall(ToolCall tool, StreamState state)
    {
        var toolCalls = state.ToolCalls;

        if (!toolCalls.TryGetValue(tool.Index, out var existing))
        {
            existing = tool with { Function = new ToolFunction() };
            toolCalls[tool.Index] = existing;
        }

        if (string.IsNullOrWhiteSpace(existing.Id) &&
            !string.IsNullOrWhiteSpace(tool.Id))
        {
            // The identifier is only sent with the first fragment; keep it whichever fragment
            // carries it so the tool result can be matched to the call.
            existing = existing with
            {
                Id = tool.Id,
                Type = tool.Type ?? existing.Type
            };

            toolCalls[tool.Index] = existing;
        }

        if (tool.Function is null)
            return;

        var function = existing.Function ?? new ToolFunction();

        if (string.IsNullOrWhiteSpace(function.Name))
            function.Name = tool.Function.Name;

        // Arguments arrive as fragments and must be concatenated in arrival order.
        function.Arguments = string.Concat(
            function.Arguments,
            tool.Function.Arguments);

        if (!ReferenceEquals(function, existing.Function))
            toolCalls[tool.Index] = existing with { Function = function };
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
                .ToLowerInvariant(),

            Content = null,
            ToolCalls = toolCalls
        });

        var executions = new List<ToolExecution>(toolCalls.Count);

        foreach (var toolCall in toolCalls)
        {
            try
            {
                executions.Add(
                    _toolScheduler.Schedule(toolCall, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single tool call that cannot be scheduled must not abort the remaining ones.
                // The failure is recorded as an execution so it is reported to the model and is
                // visible to the caller through the scheduler state.
                executions.Add(new ToolExecution
                {
                    Call = toolCall,
                    Status = ToolExecutionStatus.FAILED,
                    Exception = new CrideApiException(
                        message:
                            $"Tool call '{toolCall.Function?.Name ?? "<unknown>"}' " +
                            $"(id '{toolCall.Id}') could not be scheduled: {exception.Message}",
                        component: ComponentName,
                        operation: "Schedule a tool call for execution",
                        innerException: exception,
                        hint:
                            "Check that each tool is registered with a unique name and that the tool call arrives with a function name."),
                    CompletedAt = DateTimeOffset.UtcNow
                });
            }
        }

        var pending = executions
            .Where(execution => execution.ExecutionTask is not null)
            .Select(execution => execution.Task)
            .ToArray();

        try
        {
            await Task.WhenAll(pending);
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
                    .ToLowerInvariant(),

                ToolCallId = execution.Call.Id,
                Content = result
            });
        }

        _toolScheduler.ClearCompleted();
    }

    private static readonly JsonSerializerOptions ChunkJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
