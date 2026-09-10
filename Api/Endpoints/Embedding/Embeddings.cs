using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Text.Json;

namespace CrideLLMApi.Api.Endpoints;

public sealed class Embeddings
{
    private readonly EmbeddingRequestBuilder _requestBuilder;
    private readonly EmbeddingTransport _transport;
    private readonly EmbeddingProviderOptions _options;
    public string ModelName { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Embeddings(HttpClient httpClient, ProviderInfo providerInfo)
    {
        _requestBuilder = new EmbeddingRequestBuilder(providerInfo);
        _transport = new EmbeddingTransport(httpClient);
        _options = providerInfo.Embeddings;
    }

    /// <summary>
    /// Creates an embedding request for a single string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="model">The model to use.</param>
    /// <returns>The embedding request.</returns>
    public EmbeddingRequest CreateRequest(string input, string? model = null) => 
        _requestBuilder.Create(input, model);

    /// <summary>
    /// Creates an embedding request for a list of strings.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="model">The model to use.</param>
    /// <returns>The embedding request.</returns>
    public EmbeddingRequest CreateRequest(IEnumerable<string> input, string? model = null) => 
        _requestBuilder.Create(input, model);

    /// <summary>
    /// Embeds a request and returns the raw HttpResponseMessage.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw HTTP response message.</returns>
    public Task<HttpResponseMessage> GetRawResponseAsync(EmbeddingRequest request, CancellationToken cancellationToken = default) => 
        _transport.SendAsync(request, cancellationToken);

    /// <summary>
    /// Embeds a request and returns an EmbeddingResponse object.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding response.</returns>
    /// <exception cref="JsonException">Thrown when the embedding response is invalid.</exception>
    public async Task<EmbeddingResponse> CreateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _transport.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await JsonSerializer.DeserializeAsync<EmbeddingResponse>(
                stream,
                JsonOptions,
                cancellationToken);

        return result ?? throw new JsonException("The embedding response was empty.");
    }

    /// <summary>
    /// Embeds a single string and returns an EmbeddingResponse object.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding response.</returns>
    public Task<EmbeddingResponse> CreateAsync(string input, CancellationToken cancellationToken = default) => 
        CreateAsync(CreateRequest(input), cancellationToken);

    /// <summary>
    /// Embeds a list of strings and returns an EmbeddingResponse object.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding response.</returns>
    public Task<EmbeddingResponse> CreateAsync(IEnumerable<string> input, CancellationToken cancellationToken = default) => 
        CreateAsync(CreateRequest(input), cancellationToken);

    /// <summary>
    /// Embeds a single string and returns an EmbeddingVector object.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding vector.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the embedding response contains no vectors.</exception>
    public async Task<EmbeddingVector> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        var response = await CreateAsync(input, cancellationToken);

        if (response.Data.Count == 0)
            throw new InvalidOperationException("The embedding response contained no vectors.");

        var values = response.Data
            .OrderBy(x => x.Index)
            .First()
            .Embedding;

        var embedding = new EmbeddingVector
        {
            Values = values,
            Model = response.Model,
            OriginalDimensions = values.Length,
            IsNormalized = false
        };

        return ProcessEmbedding(embedding);
    }

    /// <summary>
    /// Embeds a list of strings and returns a list of EmbeddingVector objects.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding vectors.</returns>
    public async Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IEnumerable<string> input, CancellationToken cancellationToken = default)
    {
        var response = await CreateAsync(input, cancellationToken);

        return response.Data
            .OrderBy(x => x.Index)
            .Select(x =>
            {
                var embedding = new EmbeddingVector
                {
                    Values = x.Embedding,
                    Model = response.Model,
                    OriginalDimensions = x.Embedding.Length,
                    IsNormalized = false
                };

                return ProcessEmbedding(embedding);
            })
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
}
