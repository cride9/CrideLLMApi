using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using CrideLLMApi.VectorDB;
using System.Net.Http.Headers;

namespace CrideLLMApi.Api;

/// <summary>
/// Entry point of CrideLLMApi. Owns the configured endpoints and the optional vector database
/// extension. One instance can serve multiple conversations through independent endpoints
/// created with CreateEndpointMethods. Keep it alive until their streams have finished.
/// </summary>
public class CrideApi : IDisposable, IAsyncDisposable
{
    private const string ComponentName = "CrideApi";

    private readonly ProviderInfo _providerInfo;
    private readonly HttpClient _httpClient;
    private readonly List<object> _endpoints = new();
    private readonly object _endpointLock = new();
    private readonly IVectorDatabase? _vectorDatabase;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrideApi"/> class.
    /// <para>
    /// The supplied <see cref="ProviderInfo"/> is copied, so the library never mutates the
    /// configuration instance owned by the caller.
    /// </para>
    /// </summary>
    /// <param name="info">The provider configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="info"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the configuration is invalid: a non-absolute or non-HTTP endpoint, an
    /// unsupported chat configuration, a vector database provider that is not registered, or a
    /// vector database that fails to initialize.
    /// </exception>
    public CrideApi(ProviderInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        ValidateEndPoint(info.EndPoint);

        _providerInfo = info with
        {
            ApiMode = string.IsNullOrWhiteSpace(info.ApiKey)
                ? API_MODE.LOCAL
                : API_MODE.OPENAI_COMPATIBLE
        };

        _httpClient = new()
        {
            BaseAddress = info.EndPoint,
        };

        if (_providerInfo.ApiMode.HasFlag(API_MODE.OPENAI_COMPATIBLE))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", info.ApiKey);
        }

        // The endpoint instances are created on first use so a chat-incompatible configuration
        // (for example Chat.Streaming = false) does not prevent an embeddings-only consumer from
        // creating the client.
        _vectorDatabase = CreateVectorDatabase(_providerInfo);
    }

    /// <summary>
    /// Gets the endpoint implementation of the specified type.
    /// </summary>
    /// <typeparam name="T">
    /// The endpoint type to retrieve. The library registers the following endpoints:
    /// <see cref="ChatCompletion"/> (chat completions with streaming, tool calling and the
    /// automatic tool loop) and <see cref="Embeddings"/> (OpenAI-compatible embeddings).
    /// </typeparam>
    /// <returns>
    /// The registered instance of <typeparamref name="T"/>, or <see langword="null"/> when no
    /// endpoint of that type is registered. Prefer checking for <see langword="null"/> before
    /// using the result. The endpoint instances are created on the first call and cached.
    /// For separate conversations use <see cref="CreateEndpointMethods{T}"/> instead of
    /// replacing the context on this shared chat endpoint.
    /// </returns>
    /// <example>
    /// <code>
    /// var chatCompletion = api.GetEndpointMethods&lt;ChatCompletion&gt;();
    /// var embeddings = api.GetEndpointMethods&lt;Embeddings&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="ChatCompletion"/>
    /// <seealso cref="Embeddings"/>
    public T? GetEndpointMethods<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GetEndpoints()
            .OfType<T>()
            .FirstOrDefault();
    }

    /// <summary>
    /// Creates an independent endpoint using this API's connection and configuration.
    /// Unlike GetEndpointMethods, the instance is not cached: a chat endpoint starts with
    /// no context, tools or event handlers. Keep one per conversation and attach its own
    /// ContextManager. The owning CrideApi must outlive all endpoints and active streams.
    /// </summary>
    /// <typeparam name="T">ChatCompletion or Embeddings.</typeparam>
    /// <returns>A fresh endpoint, or null for an unsupported type.</returns>
    public T? CreateEndpointMethods<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (typeof(T) == typeof(ChatCompletion))
            return new ChatCompletion(_httpClient, _providerInfo) as T;
        if (typeof(T) == typeof(Embeddings))
            return new Embeddings(_httpClient, _providerInfo) as T;
        return null;
    }

    /// <summary>
    /// Gets the extension instance of the specified type.
    /// </summary>
    /// <typeparam name="T">
    /// The extension type to retrieve. The only extension family the library exposes is the
    /// vector database (<see cref="IVectorDatabase"/>), created from
    /// <see cref="ExtensionProviderOptions.VectorDB"/>. An instance is only available when
    /// <see cref="ExtensionProviderOptions.VectorDB"/> is not <see cref="VectorDatabaseType.NONE"/>
    /// and a provider for that type has been registered with
    /// <see cref="VectorDatabaseRegistry.Register"/>. Extension packages register themselves
    /// automatically: <c>CrideLLMApi.VectorDB.ChromaDB</c> registers
    /// <c>CrideLLMApi.VectorDB.ChromaDB.ChromaDatabase</c> for
    /// <see cref="VectorDatabaseType.CHROMADB"/>. No provider ships for
    /// <see cref="VectorDatabaseType.QDRANT"/> at this time.
    /// </typeparam>
    /// <returns>
    /// The created vector database cast to <typeparamref name="T"/>, or <see langword="null"/>
    /// when no vector database is configured or when the configured instance is not assignable to
    /// <typeparamref name="T"/>. Request the concrete type of the configured provider, for
    /// example <c>GetExtension&lt;ChromaDatabase&gt;()</c>, or the interface when a
    /// provider-agnostic access is enough.
    /// </returns>
    /// <example>
    /// <code>
    /// var database = api.GetExtension&lt;ChromaDatabase&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="IVectorDatabase"/>
    /// <seealso cref="VectorDatabaseRegistry.Register"/>
    public T? GetExtension<T>() where T : class
    {
        return _vectorDatabase as T;
    }

    /// <summary>
    /// Releases the HTTP client and the vector database extension. An extension that only supports
    /// asynchronous disposal is disposed synchronously as a best effort, so it is never silently
    /// leaked; prefer <see cref="DisposeAsync"/> to avoid blocking the calling thread.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_vectorDatabase is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (_vectorDatabase is IAsyncDisposable asyncDisposable)
        {
            // Blocking on purpose: Dispose cannot await, and skipping the extension would leave
            // its resources (for example a managed vector database process) behind.
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        _httpClient.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the HTTP client and awaits the asynchronous disposal of the vector database
    /// extension when it supports it. A synchronous-only extension is disposed synchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_vectorDatabase is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_vectorDatabase is IDisposable disposable)
            disposable.Dispose();

        _httpClient.Dispose();

        GC.SuppressFinalize(this);
    }

    private List<object> GetEndpoints()
    {
        lock (_endpointLock)
        {
            if (_endpoints.Count == 0)
            {
                var chatCompletion = new ChatCompletion(_httpClient, _providerInfo);
                var embeddings = new Embeddings(_httpClient, _providerInfo);

                _endpoints.Add(chatCompletion);
                _endpoints.Add(embeddings);
            }

            return _endpoints;
        }
    }

    private static void ValidateEndPoint(Uri endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        if (endPoint.IsAbsoluteUri &&
            (endPoint.Scheme == Uri.UriSchemeHttp || endPoint.Scheme == Uri.UriSchemeHttps))
        {
            return;
        }

        throw new CrideApiException(
            message:
                $"ProviderInfo.EndPoint '{endPoint}' is not a usable provider address: the endpoint must be an absolute HTTP or HTTPS URI.",
            component: ComponentName,
            operation: "Create the API client",
            hint:
                "Set ProviderInfo.EndPoint to the base address of the server, for example new Uri(\"http://127.0.0.1:8080\").");
    }

    private IVectorDatabase? CreateVectorDatabase(ProviderInfo providerInfo)
    {
        try
        {
            return VectorDatabaseRegistry.Create(
                providerInfo.Extensions.VectorDB,
                this,
                providerInfo);
        }
        catch (CrideApiException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CrideApiException(
                message:
                    $"The vector database extension '{providerInfo.Extensions.VectorDB}' could not be created: {exception.Message}",
                component: ComponentName,
                operation: "Create the vector database extension",
                innerException: exception,
                hint:
                    "Check the ExtensionProviderOptions values (VectorDBEndPoint, VectorDBApiKey, VectorDBRuntimePath, VectorDBDataPath) and that the extension package for the selected VectorDatabaseType is installed.");
        }
    }
}
