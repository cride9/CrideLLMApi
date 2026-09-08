using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CrideLLMApi.Api.CrideApi;

namespace CrideLLMApi.Api.Endpoints;

public class ChatCompletion
{
    public event Action<ToolCall, string>? ToolCallExecuted;
    private ContextManager? _contextManager;
    private Dictionary<int, ToolCall> _toolCalls = new();
    private List<FunctionCallObject> _apiFunctions = new();
    private string? _lastFinishReason;
    private ProviderInfo _llmInfo;
    private HttpClient _httpClient;
    private readonly Dictionary<string, ToolExecutor> _toolHandlers = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ChatCompletion(ProviderInfo lLMInfo, HttpClient httpClient)
    {
        _llmInfo = lLMInfo;
        _httpClient = httpClient;
    }

    public IEnumerable<FunctionCallObject> GetFunctionCalls() =>
        _apiFunctions.AsReadOnly();

    public async IAsyncEnumerable<Delta> GetResponseAsync(string message)
    {
        var response = GetRawResponseAsync(message);
        await foreach (var item in response)
        {
            if (!string.IsNullOrWhiteSpace(item?.Choices[0]?.Delta?.Content))
                yield return item.Choices[0].Delta!;
        }
    }

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

    public void AddFunction(ApiFunction function, ToolExecutor? handler = null)
    {
        _apiFunctions.Add(function.AsFunctionCall( ));

        if ( handler != null )
            _toolHandlers[ function.Name ] = handler;
    }

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
