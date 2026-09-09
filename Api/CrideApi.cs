using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Net.Http.Headers;

namespace CrideLLMApi.Api;

public class CrideApi : IDisposable
{
    private ProviderInfo _llmInfo;
    private HttpClient _httpClient;
    private List<object> _endpoints;

    public CrideApi(Uri endpoint, string? apiKey = null, string? modelName = null, bool streaming = true) =>
        InitializeEndpoint(new ProviderInfo() { EndPoint = endpoint, ApiKey = apiKey, ModelName = modelName, Streaming = streaming });
    public CrideApi(string endpoint, string? apiKey = null, string? modelName = null, bool streaming = true) =>
        InitializeEndpoint(new ProviderInfo() { EndPoint = new Uri(endpoint), ApiKey = apiKey, ModelName = modelName, Streaming = streaming });
    public CrideApi(ProviderInfo lLMInfo) =>
        InitializeEndpoint(lLMInfo);

    /// <summary>
    /// Gets the endpoint methods for the specified type.
    /// </summary>
    /// <typeparam name="T">The class of the endpoint methods to retrieve. (eg.: ChatCompletion)</typeparam>
    /// <returns>Retrieves the endpoint methods for the specified class.</returns>
    public T? GetEndpointMethods<T>() where T : class
    {
        return _endpoints
            .OfType<T>()
            .FirstOrDefault();
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
            new ChatCompletion(_httpClient, _llmInfo),
            new Embeddings(_httpClient, _llmInfo)
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
