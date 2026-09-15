using CrideLLMApi.DTO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrideLLMApi.Api.Endpoints;

/// <summary>
/// Sends chat completion requests to the OpenAI-compatible provider.
/// <para>
/// Failures are reported as <see cref="Helpers.CrideApiException"/> so the caller always knows
/// which operation failed, with which status and what to change. Cancellation requested by the
/// caller is never converted into an API error.
/// </para>
/// </summary>
internal sealed class ChatCompletionTransport
{
    private const string ChatCompletionsPath = "v1/chat/completions";
    private const string ComponentName = "ChatCompletion";

    private const string RequestShapeHint =
        "The provider rejected the chat request payload. Check ProviderInfo.Chat.ModelName, " +
        "the conversation messages, the registered tool definitions and the " +
        "Chat.ReasoningEffort / Chat.ToolChoice values.";

    private readonly HttpClient _httpClient;
    private readonly ProviderInfo _providerInfo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull
    };

    public ChatCompletionTransport(
        HttpClient httpClient,
        ProviderInfo providerInfo)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(providerInfo);

        _httpClient = httpClient;
        _providerInfo = providerInfo;
    }

    /// <summary>
    /// Gets the description of the operation performed by this transport, used for error context.
    /// </summary>
    internal string Operation =>
        $"POST '{ChatCompletionsPath}' to '{_httpClient.BaseAddress}'";

    /// <summary>
    /// Sends a request to the OpenAI-compatible chat completions endpoint and guarantees that a
    /// successful response is returned. Any other outcome throws
    /// <see cref="Helpers.CrideApiException"/>.
    /// </summary>
    /// <param name="requestBody">The request body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The successful HTTP response message.</returns>
    /// <exception cref="Helpers.CrideApiException">
    /// Thrown when the provider returns an unsuccessful status code, when the connection fails,
    /// or when the request times out.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the request through <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task<HttpResponseMessage> SendAsync(
        object requestBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            ChatCompletionsPath);

        request.Content =
            JsonContent.Create(
                requestBody,
                options: JsonOptions);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller cancelled the operation: keep the cancellation semantics instead of
            // reporting it as a provider failure.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            throw ApiResponseValidator.CreateTransportException(
                exception,
                ComponentName,
                Operation,
                _providerInfo.Chat.ModelName,
                requestTimedOut: exception is OperationCanceledException);
        }

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            var failure = await ApiResponseValidator.CreateFailureExceptionAsync(
                response,
                ComponentName,
                Operation,
                _providerInfo.Chat.ModelName,
                RequestShapeHint,
                cancellationToken);

            throw failure;
        }
    }
}
