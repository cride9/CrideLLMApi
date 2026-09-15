using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using CrideLLMApi.VectorDB;
using System.Net.Http.Headers;

namespace CrideLLMApi.Api;

public class CrideApi : IDisposable
{
    private readonly ProviderInfo _providerInfo;
    private HttpClient _httpClient;
    private List<object> _endpoints;
    private readonly IVectorDatabase? _vectorDatabase;

    public CrideApi(ProviderInfo info)
    {
        _providerInfo = info;
        _providerInfo.ApiMode = string.IsNullOrWhiteSpace(info.ApiKey) ? API_MODE.LOCAL : API_MODE.OPENAI_COMPATIBLE;

        _httpClient = new()
        {
            BaseAddress = info.EndPoint,
        };

        if (_providerInfo.ApiMode.HasFlag(API_MODE.OPENAI_COMPATIBLE))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", info.ApiKey);
        }

        _endpoints = new()
        {
            new ChatCompletion(_httpClient, _providerInfo),
            new Embeddings(_httpClient, _providerInfo)
        };

        _vectorDatabase =
            VectorDatabaseRegistry.Create(
                _providerInfo.Extensions.VectorDB,
                this,
                _providerInfo);
    }

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

    /// <summary>
    /// Gets the extension of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the extension to retrieve.</typeparam>
    /// <returns>The extension of the specified type, or null if not found.</returns>
    public T? GetExtension<T>() where T : class
    {
        return _vectorDatabase as T;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
