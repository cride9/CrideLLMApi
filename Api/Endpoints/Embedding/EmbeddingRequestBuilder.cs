using CrideLLMApi.DTO;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class EmbeddingRequestBuilder
{
    private readonly ProviderInfo _providerInfo;

    public EmbeddingRequestBuilder(ProviderInfo providerInfo)
    {
        _providerInfo = providerInfo;
    }

    /// <summary>
    /// Creates an EmbeddingRequest with a single input.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="model">The model to use.</param>
    /// <returns>The embedding request.</returns>
    /// <exception cref="ArgumentException">Thrown when the input is invalid.</exception>
    public EmbeddingRequest Create(string input, string? model = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        return new EmbeddingRequest
        {
            Model = model ?? _providerInfo.ModelName,
            Input = input
        };
    }

    /// <summary>
    /// Creates an EmbeddingRequest with multiple inputs.
    /// </summary>
    /// <param name="input">The input strings.</param>
    /// <param name="model">The model to use.</param>
    /// <returns>The embedding request.</returns>
    /// <exception cref="ArgumentException">Thrown when the input is invalid.</exception>
    public EmbeddingRequest Create(IEnumerable<string> input, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var inputs = input.ToArray();

        if (inputs.Length == 0)
            throw new ArgumentException(
                "At least one input is required.",
                nameof(input));

        return new EmbeddingRequest
        {
            Model = model ?? _providerInfo.ModelName,
            Input = inputs
        };
    }
}
