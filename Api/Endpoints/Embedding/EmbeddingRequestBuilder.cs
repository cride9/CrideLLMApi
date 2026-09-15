using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class EmbeddingRequestBuilder
{
    private const string ComponentName = "EmbeddingRequestBuilder";

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
    /// <exception cref="CrideApiException">
    /// Thrown when the configured embedding options or the provider mode require a value that
    /// is missing or invalid.
    /// </exception>
    public EmbeddingRequest Create(string input, string? model = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        ValidateModelArgument(model);
        ValidateOptions(model);

        return new EmbeddingRequest
        {
            Model = model ?? _providerInfo.Embeddings.ModelName,
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
    /// <exception cref="CrideApiException">
    /// Thrown when the configured embedding options or the provider mode require a value that
    /// is missing or invalid.
    /// </exception>
    public EmbeddingRequest Create(IEnumerable<string> input, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateModelArgument(model);
        ValidateOptions(model);

        var inputs = input.ToArray();

        if (inputs.Length == 0)
        {
            throw new ArgumentException(
                "At least one input is required.",
                nameof(input));
        }

        if (inputs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Embedding inputs cannot contain null, empty, or whitespace-only strings.",
                nameof(input));
        }

        return new EmbeddingRequest
        {
            Model = model ?? _providerInfo.Embeddings.ModelName,
            Input = inputs
        };
    }

    /// <summary>
    /// Validates the embedding provider options before a request is sent, so a configuration
    /// mistake fails fast with the option name instead of an opaque provider error.
    /// </summary>
    /// <param name="model">
    /// The explicit per-request model name, when the caller supplied one. An explicit model is
    /// sent instead of the configured one, so the configuration does not have to carry a model
    /// name in that case.
    /// </param>
    /// <exception cref="CrideApiException">Thrown when the configuration is invalid.</exception>
    private void ValidateOptions(string? model = null)
    {
        var options = _providerInfo.Embeddings;

        if (options.TargetDimensions is int targetDimensions &&
            targetDimensions <= 0)
        {
            throw new CrideApiException(
                message:
                    $"ProviderInfo.Embeddings.TargetDimensions is {targetDimensions}, which is not a valid dimension count.",
                component: ComponentName,
                operation: "Build the embedding request",
                hint:
                    "Set ProviderInfo.Embeddings.TargetDimensions to a positive value or leave it null to keep the provider vector size.");
        }

        // The request carries its own model name, so the configured one is not required.
        if (!string.IsNullOrWhiteSpace(model))
            return;

        if (!_providerInfo.ApiMode.HasFlag(API_MODE.OPENAI_COMPATIBLE))
            return;

        if (!string.IsNullOrWhiteSpace(options.ModelName))
            return;

        throw new CrideApiException(
            message:
                "ProviderInfo.Embeddings.ModelName is not set, but the OpenAI-compatible provider mode requires an explicit model name.",
            component: ComponentName,
            operation: "Build the embedding request",
            hint:
                "Set ProviderInfo.Embeddings.ModelName to the identifier of the embedding model served by ProviderInfo.EndPoint.");
    }

    /// <summary>
    /// Validates an explicitly supplied model name.
    /// </summary>
    /// <param name="model">The model name supplied by the caller.</param>
    /// <exception cref="ArgumentException">Thrown when the supplied model name is whitespace.</exception>
    private static void ValidateModelArgument(string? model)
    {
        if (model is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(model);
    }
}
