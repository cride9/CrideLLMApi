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
    private Dictionary<API_ENDPOINT, dynamic> _endpoints;

    public CrideApi(Uri endpoint, string? apiKey = null, string? modelName = null, bool streaming = true) =>
        InitializeEndpoint(new ProviderInfo() { EndPoint = endpoint, ApiKey = apiKey, ModelName = modelName, Streaming = streaming });
    public CrideApi(string endpoint, string? apiKey = null, string? modelName = null, bool streaming = true) =>
        InitializeEndpoint(new ProviderInfo() { EndPoint = new Uri(endpoint), ApiKey = apiKey, ModelName = modelName, Streaming = streaming });
    public CrideApi(ProviderInfo lLMInfo) =>
        InitializeEndpoint(lLMInfo);

    public T? GetEndpointMethods<T>( )
    {
        return (T?) _endpoints.Values
            .FirstOrDefault(x => x.GetType( ) == typeof(T));
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
            { 
                API_ENDPOINT.CHAT_COMPLETION, 
                new ChatCompletion(
                    _llmInfo,
                    _httpClient)
            }
        };
    }
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
