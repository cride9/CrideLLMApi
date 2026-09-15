using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Text.Json;

namespace CrideLLMApi.Api.Endpoints;

public sealed class Embeddings
{
    private const string ComponentName = "Embeddings";

    private const string ResponseShapeHint =
        "Check ProviderInfo.Embeddings.ModelName and that the provider returns an " +
        "OpenAI-compatible embeddings payload with a non-empty 'data' array.";

    private readonly EmbeddingRequestBuilder _requestBuilder;
    private readonly EmbeddingTransport _transport;
    private readonly EmbeddingProviderOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Embeddings"/> endpoint.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for provider communication.</param>
    /// <param name="providerInfo">The provider configuration.</param>
    public Embeddings(HttpClient httpClient, ProviderInfo providerInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(providerInfo);

        _requestBuilder = new EmbeddingRequestBuilder(providerInfo);
        _transport = new EmbeddingTransport(httpClient, providerInfo);
        _options = providerInfo.Embeddings;
    }

    /// <summary>
    /// Creates an embedding request for a single string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="model">The model to use.</param>
    /// <returns>The embedding request.</returns>
    /// <exception cref="ArgumentException">Thrown when the input is null, empty or whitespace.</exception>
    public EmbeddingRequest CreateRequest(string input, string? model = null) => 
        _requestBuilder.Create(input, model);

    /// <summary>
    /// Creates an embedding request for a list of strings.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="model">The model to use.</param>
    /// <returns>The embedding request.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the input collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input collection is empty or contains null, empty or whitespace-only strings.</exception>
    public EmbeddingRequest CreateRequest(IEnumerable<string> input, string? model = null) => 
        _requestBuilder.Create(input, model);

    /// <summary>
    /// Sends an embedding request and returns the raw HTTP response, which is guaranteed to have
    /// a successful status code.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw HTTP response message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider returns an unsuccessful status code, when the connection fails,
    /// or when the request times out.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation through <paramref name="cancellationToken"/>.
    /// </exception>
    public Task<HttpResponseMessage> GetRawResponseAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _transport.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Embeds a request and returns an EmbeddingResponse object.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider fails, when the response cannot be deserialized, or when the
    /// response carries no usable vectors.
    /// </exception>
    public async Task<EmbeddingResponse> CreateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _transport.SendAsync(request, cancellationToken);

        var result = await DeserializeResponseAsync(response, cancellationToken);

        ValidateResponse(result);

        return result;
    }

    /// <summary>
    /// Embeds a single string and returns an EmbeddingResponse object.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding response.</returns>
    /// <exception cref="ArgumentException">Thrown when the input is null, empty or whitespace.</exception>
    /// <exception cref="CrideApiException">Thrown when the provider fails or returns no usable vectors.</exception>
    public Task<EmbeddingResponse> CreateAsync(string input, CancellationToken cancellationToken = default) => 
        CreateAsync(CreateRequest(input), cancellationToken);

    /// <summary>
    /// Embeds a list of strings and returns an EmbeddingResponse object.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the input collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input collection is empty or contains null, empty or whitespace-only strings.</exception>
    /// <exception cref="CrideApiException">Thrown when the provider fails or returns no usable vectors.</exception>
    public Task<EmbeddingResponse> CreateAsync(IEnumerable<string> input, CancellationToken cancellationToken = default) => 
        CreateAsync(CreateRequest(input), cancellationToken);

    /// <summary>
    /// Embeds a single string and returns an EmbeddingVector object.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding vector.</returns>
    /// <exception cref="ArgumentException">Thrown when the input is null, empty or whitespace.</exception>
    /// <exception cref="CrideApiException">Thrown when the provider fails or returns no usable vectors.</exception>
    public async Task<EmbeddingVector> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var response = await CreateAsync(input, cancellationToken);

        return CreateVector(
            response.Data.OrderBy(x => x.Index).First(),
            response.Model);
    }

    /// <summary>
    /// Embeds a list of strings and returns a list of EmbeddingVector objects.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding vectors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the input collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input collection is empty or contains null, empty or whitespace-only strings.</exception>
    /// <exception cref="CrideApiException">Thrown when the provider fails or returns no usable vectors.</exception>
    public async Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IEnumerable<string> input, CancellationToken cancellationToken = default)
    {
        var response = await CreateAsync(input, cancellationToken);

        return response.Data
            .OrderBy(x => x.Index)
            .Select(x => CreateVector(x, response.Model))
            .ToArray();
    }

    /// <summary>
    /// Processes an embedding vector according to the provider's options, such as normalizing and adjusting dimensions.
    /// </summary>
    /// <param name="embedding">The embedding vector to process.</param>
    /// <returns>The processed embedding vector.</returns>
    private EmbeddingVector ProcessEmbedding(EmbeddingVector embedding)
    {
        if (_options.TargetDimensions is int targetDimensions)
            embedding = embedding.WithDimensions(targetDimensions);

        if (_options.Normalize)
            embedding = embedding.Normalize();

        return embedding;
    }

    /// <summary>
    /// Creates an EmbeddingVector from EmbeddingData and model information.
    /// </summary>
    /// <param name="data">The embedding data.</param>
    /// <param name="model">The model information.</param>
    /// <returns>The created embedding vector.</returns>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider returned no vector values for the entry, or when the configured
    /// target dimensions are not valid for the returned vector size.
    /// </exception>
    private EmbeddingVector CreateVector(EmbeddingData data, string? model)
    {
        var values = data.Embedding;

        if (values is null || values.Length == 0)
        {
            throw new CrideApiException(
                message:
                    $"The embedding entry at index {data.Index} contains no vector values.",
                component: ComponentName,
                operation: "Process the embedding response",
                hint: ResponseShapeHint);
        }

        ValidateTargetDimensions(values.Length);

        return ProcessEmbedding(new EmbeddingVector
        {
            Values = values,
            Model = model,
            OriginalDimensions = values.Length,
            IsNormalized = false
        });
    }

    /// <summary>
    /// Validates the configured target dimensions against the vector size returned by the
    /// provider, so a configuration mistake is reported with the option name instead of failing
    /// deep inside the vector arithmetic.
    /// </summary>
    /// <param name="providerDimensions">The dimensionality returned by the provider.</param>
    /// <exception cref="CrideApiException">Thrown when the configured dimensions cannot be applied.</exception>
    private void ValidateTargetDimensions(int providerDimensions)
    {
        if (_options.TargetDimensions is not int targetDimensions)
            return;

        if (targetDimensions <= 0)
        {
            throw new CrideApiException(
                message:
                    $"ProviderInfo.Embeddings.TargetDimensions is {targetDimensions}, which is not a valid dimension count.",
                component: ComponentName,
                operation: "Process the embedding response",
                hint:
                    "Set ProviderInfo.Embeddings.TargetDimensions to a positive value or leave it null to keep the provider vector size.");
        }

        if (targetDimensions > providerDimensions)
        {
            throw new CrideApiException(
                message:
                    $"ProviderInfo.Embeddings.TargetDimensions is {targetDimensions}, but the provider returned {providerDimensions}-dimensional vectors, and dimensions can only be truncated, never increased.",
                component: ComponentName,
                operation: "Process the embedding response",
                hint:
                    "Set ProviderInfo.Embeddings.TargetDimensions to at most the provider vector size, or leave it null to keep the full vector.");
        }
    }

    /// <summary>
    /// Deserializes the embedding response, reporting provider payload problems with the
    /// component, the operation and the offending payload.
    /// </summary>
    /// <param name="response">The successful HTTP response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    /// <exception cref="CrideApiException">Thrown when the response body cannot be deserialized.</exception>
    private async Task<EmbeddingResponse> DeserializeResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        EmbeddingResponse? result;

        try
        {
            result = await JsonSerializer.DeserializeAsync<EmbeddingResponse>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new CrideApiException(
                message:
                    "The embedding response sent by the provider could not be deserialized.",
                component: ComponentName,
                operation: _transport.Operation,
                innerException: exception,
                hint:
                    "Compare the provider response with the OpenAI embeddings response format. " +
                    "The provider may be returning a different payload shape.");
        }

        return result ?? throw new CrideApiException(
            message: "The embedding response was empty.",
            component: ComponentName,
            operation: _transport.Operation,
            hint: ResponseShapeHint);
    }

    /// <summary>
    /// Validates that the response carries at least one usable vector.
    /// </summary>
    /// <param name="response">The deserialized response.</param>
    /// <exception cref="CrideApiException">Thrown when no usable vectors are present.</exception>
    private static void ValidateResponse(EmbeddingResponse response)
    {
        var data = response.Data;

        if (data is null || data.Count == 0)
        {
            throw new CrideApiException(
                message:
                    "The embedding response contained no vectors.",
                component: ComponentName,
                operation: "Process the embedding response",
                hint: ResponseShapeHint);
        }

        for (var index = 0; index < data.Count; index++)
        {
            if (data[index] is null)
            {
                throw new CrideApiException(
                    message:
                        $"The embedding response contains a null entry at position {index}.",
                    component: ComponentName,
                    operation: "Process the embedding response",
                    hint: ResponseShapeHint);
            }
        }
    }
}
