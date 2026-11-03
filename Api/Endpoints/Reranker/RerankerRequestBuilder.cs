using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class RerankerRequestBuilder
{
    private readonly ProviderInfo _providerInfo;

    public RerankerRequestBuilder(ProviderInfo providerInfo)
    {
        _providerInfo = providerInfo;
    }

    public RerankRequest Create(string query, IEnumerable<string> documents, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var request = new RerankRequest
        {
            Model = model ?? _providerInfo.Reranker.ModelName,
            Query = query,
            Documents = documents.ToArray(),
            TopN = _providerInfo.Reranker.TopN
        };

        Validate(request);
        return request;
    }

    public void Validate(RerankRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);
        ArgumentNullException.ThrowIfNull(request.Documents);

        if (request.Documents.Count == 0 || request.Documents.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty document is required; documents cannot contain blank strings.", nameof(request));

        if (request.TopN is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "TopN must be positive when set.");

        if (request.Model is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);

        if (_providerInfo.ApiMode.HasFlag(API_MODE.OPENAI_COMPATIBLE) && request.Model is null)
        {
            throw new CrideApiException(
                message: "The authenticated provider requires an explicit reranking model name.",
                component: "RerankerRequestBuilder",
                operation: "Build the reranking request",
                hint: "Set ProviderInfo.Reranker.ModelName or specify the model on the request.");
        }
    }
}
