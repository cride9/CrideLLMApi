using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Net.Http.Headers;
using System.Reflection;

namespace CrideLLMApi.Api;

public enum API_ENDPOINT
{
    CHAT_COMPLETION,
    EMBEDDING,
    MODELS
}

public class CrideApi : IDisposable
{
    public delegate Task<string> ToolExecutor(string argumentsJson);
    private ProviderInfo _llmInfo;
    private HttpClient _httpClient;
    public event Action<ToolCall, string>? ToolCallExecuted;
    private Dictionary<API_ENDPOINT, dynamic> _endpoints; // dynamic bad practice but idc now
    private List<ChatMessage> _chatHistory;
    public CrideApi(Uri endpoint, string? apiKey = null, string? modelName = null, bool streaming = true) =>
        InitializeEndpoint(new ProviderInfo() { EndPoint = endpoint, ApiKey = apiKey, ModelName = modelName, Streaming = streaming });
    public CrideApi(string endpoint, string? apiKey = null, string? modelName = null, bool streaming = true) =>
        InitializeEndpoint(new ProviderInfo() { EndPoint = new Uri(endpoint), ApiKey = apiKey, ModelName = modelName, Streaming = streaming });
    public CrideApi(ProviderInfo lLMInfo) =>
        InitializeEndpoint(lLMInfo);

    public IEnumerable<FunctionCallObject> GetFunctionCalls() =>
        _endpoints[API_ENDPOINT.CHAT_COMPLETION].GetFunctionCalls();
    public void AddFunctionCall(ApiFunction function, ToolExecutor? handler = null) =>
        _endpoints[API_ENDPOINT.CHAT_COMPLETION].AddFunction(function, handler);
    public void AddFunctionCalls(IEnumerable<(ApiFunction Function, ToolExecutor? Handler)> functions) =>
        _endpoints[API_ENDPOINT.CHAT_COMPLETION].AddFunctionCalls(functions);

    public List<ChatMessage> NewMemory() =>
        _chatHistory = new List<ChatMessage>();
    public List<ChatMessage> NewMemory(List<ChatMessage> memory) =>
        _chatHistory = memory;
    public List<ChatMessage>? GetMemory() =>
        _chatHistory.Select(x => x with { }).ToList();

    public async IAsyncEnumerable<Delta> GetResponseAsync(string message)
    {
        var stream = (IAsyncEnumerable<Delta>)
            _endpoints[API_ENDPOINT.CHAT_COMPLETION].GetResponseAsync(message);

        await foreach (var delta in stream)
        {
            yield return delta;
        }
    }
    public async IAsyncEnumerable<ChatCompletionChunk> GetRawResponseAsync(string message)
    {
        var stream = (IAsyncEnumerable<ChatCompletionChunk>)
            _endpoints[API_ENDPOINT.CHAT_COMPLETION].GetRawResponseAsync(message);

        await foreach (var chunk in stream)
        {
            yield return chunk;
        }
    }
    private void InitializeEndpoint(ProviderInfo info)
    {
        _llmInfo = info;
        _llmInfo.ApiMode = string.IsNullOrWhiteSpace(info.ApiKey) ? API_MODE.LOCAL : API_MODE.OPENAI_COMPATIBLE;

        _httpClient = new()
        {
            BaseAddress = info.EndPoint,
        };
        if (_llmInfo.ApiMode.HasFlag(API_MODE.OPENAI_COMPATIBLE))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", info.ApiKey);
        }
        _endpoints = new()
        {
            { API_ENDPOINT.CHAT_COMPLETION, new ChatCompletion(_llmInfo, _httpClient, _chatHistory ?? new List<ChatMessage>(), (tool, result) => ToolCallExecuted?.Invoke(tool, result)) }
        };
    }
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
