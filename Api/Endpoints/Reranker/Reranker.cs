using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Text.Json;

namespace CrideLLMApi.Api.Endpoints;

/// <summary>
/// Ranks documents against a query through POST /v1/rerank. Independent requests may run concurrently.
/// </summary>
public sealed class Reranker
{
    private readonly RerankerRequestBuilder _requestBuilder;
    private readonly RerankerTransport _transport;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Reranker(HttpClient httpClient, ProviderInfo providerInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(providerInfo);

        _requestBuilder = new RerankerRequestBuilder(providerInfo);
        _transport = new RerankerTransport(httpClient, providerInfo);
    }

    /// <summary>Creates a request using the configured model and result count.</summary>
    public RerankRequest CreateRequest(string query, IEnumerable<string> documents, string? model = null) =>
        _requestBuilder.Create(query, documents, model);

    /// <summary>Sends a request. The caller must dispose the returned HTTP response.</summary>
    public Task<HttpResponseMessage> GetRawResponseAsync(RerankRequest request, CancellationToken cancellationToken = default)
    {
        _requestBuilder.Validate(request);
        return _transport.SendAsync(request, cancellationToken);
    }

    /// <summary>Returns the provider's ranked results, retaining original document indices.</summary>
    public async Task<RerankResponse> CreateAsync(RerankRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await GetRawResponseAsync(request, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        RerankResponse? result;
        try
        {
            result = await JsonSerializer.DeserializeAsync<RerankResponse>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new CrideApiException(
                message: "The reranking response could not be deserialized.",
                component: "Reranker",
                operation: _transport.Operation,
                innerException: exception,
                hint: "Expected a results array containing index and relevance_score fields.");
        }

        if (result?.Results is null || result.Results.Count == 0 ||
            result.Results.Any(x => x is null || x.Index < 0 || x.Index >= request.Documents.Count || !double.IsFinite(x.RelevanceScore)) ||
            result.Results.Select(x => x.Index).Distinct().Count() != result.Results.Count)
        {
            throw new CrideApiException(
                message: "The reranking response contains no usable results or invalid document indices or scores.",
                component: "Reranker",
                operation: _transport.Operation,
                hint: "Check that the provider returns unique original document indices and finite relevance_score values.");
        }

        return result;
    }

    public Task<RerankResponse> CreateAsync(string query, IEnumerable<string> documents, CancellationToken cancellationToken = default) =>
        CreateAsync(CreateRequest(query, documents), cancellationToken);

    /// <summary>Returns documents' scores in descending relevance order.</summary>
    public async Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IEnumerable<string> documents, CancellationToken cancellationToken = default)
    {
        var response = await CreateAsync(query, documents, cancellationToken);
        return response.Results.OrderByDescending(x => x.RelevanceScore).ToArray();
    }
}
