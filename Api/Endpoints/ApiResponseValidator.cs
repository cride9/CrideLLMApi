using System.Net;
using System.Text.Json;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Endpoints;

/// <summary>
/// Shared HTTP failure handling for the OpenAI-compatible transports.
/// <para>
/// Every non-success response is converted into a <see cref="CrideApiException"/> that carries
/// the status code, the provider error message (when the body exposes one), the component, the
/// operation and an actionable hint, so the HTTP context is never lost.
/// </para>
/// </summary>
internal static class ApiResponseValidator
{
    private const int MaxProviderMessageLength = 512;

    /// <summary>
    /// Builds the exception for a non-success HTTP response. The caller is expected to dispose
    /// the response after this method returns.
    /// </summary>
    /// <param name="response">The failed response.</param>
    /// <param name="component">The library component reporting the failure.</param>
    /// <param name="operation">The failed operation, including the target address.</param>
    /// <param name="model">The configured model name, when there is one.</param>
    /// <param name="requestShapeHint">The component specific hint used for HTTP 400.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The exception describing the failure.</returns>
    public static async Task<CrideApiException> CreateFailureExceptionAsync(
        HttpResponseMessage response,
        string component,
        string operation,
        string? model,
        string? requestShapeHint,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(response, cancellationToken);
        var providerMessage = TryExtractProviderMessage(body);

        var modelSuffix = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : $" Configured model: '{model}'.";

        var message =
            $"The provider failed the request with HTTP {(int)response.StatusCode} ({response.StatusCode})." +
            modelSuffix +
            (providerMessage is null
                ? string.Empty
                : $" Provider message: \"{providerMessage}\"");

        return new CrideApiException(
            message: message,
            component: component,
            operation: operation,
            statusCode: response.StatusCode,
            responseBody: body,
            hint: CreateHint(response.StatusCode, requestShapeHint));
    }

    /// <summary>
    /// Builds the exception for a transport level failure, for example a refused connection or
    /// a client timeout. Caller initiated cancellation must NOT be routed here.
    /// </summary>
    /// <param name="exception">The original transport exception.</param>
    /// <param name="component">The library component reporting the failure.</param>
    /// <param name="operation">The failed operation, including the target address.</param>
    /// <param name="model">The configured model name, when there is one.</param>
    /// <param name="requestTimedOut">Whether the request timed out instead of failing to connect.</param>
    /// <returns>The exception describing the failure.</returns>
    public static CrideApiException CreateTransportException(
        Exception exception,
        string component,
        string operation,
        string? model,
        bool requestTimedOut)
    {
        var modelSuffix = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : $" Configured model: '{model}'.";

        if (requestTimedOut)
        {
            return new CrideApiException(
                message:
                    "The provider did not respond before the HTTP client timeout elapsed (100 seconds by default)." +
                    modelSuffix,
                component: component,
                operation: operation,
                innerException: exception,
                hint:
                    "Check that the provider is running and not blocked by a model load or a long queue, then retry the request.");
        }

        return new CrideApiException(
            message:
                $"The HTTP request to the provider failed: {exception.Message}" +
                modelSuffix,
            component: component,
            operation: operation,
            innerException: exception,
            hint:
                "Check that ProviderInfo.EndPoint points to a running OpenAI-compatible server and that the address is reachable from this process.");
    }

    private static string? CreateHint(HttpStatusCode statusCode, string? requestShapeHint)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest =>
                requestShapeHint ??
                "The provider rejected the request payload. Check the configured model name and the request content.",

            HttpStatusCode.Unauthorized =>
                "The provider rejected the credentials. Set ProviderInfo.ApiKey to a valid key for this endpoint.",

            HttpStatusCode.Forbidden =>
                "The provider refused access for the configured credentials or model. Check the key permissions and the model name.",

            HttpStatusCode.NotFound =>
                "The endpoint path was not found. Check that ProviderInfo.EndPoint is the server base address (for example 'http://127.0.0.1:8080') and that the server implements the OpenAI-compatible '/v1/...' routes.",

            HttpStatusCode.RequestEntityTooLarge =>
                "The request payload is too large. Reduce the conversation history, the number of tools, or the size of image inputs.",

            HttpStatusCode.RequestTimeout =>
                "The provider did not answer in time. Check that the model is loaded and that the server is not overloaded, then retry.",

            HttpStatusCode.TooManyRequests =>
                "The provider is rate limiting the requests. Retry after a delay or use a provider with a higher quota.",

            HttpStatusCode.UnprocessableEntity =>
                "The provider could not process the request content. Check the model name and the request payload.",

            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout =>
                "The provider failed while processing the request. Inspect the provider server logs; the response body above contains its error message when available.",

            _ => null
        };
    }

    private static async Task<string?> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // The body is diagnostic only: failing to read it must not hide the HTTP failure
            // that is about to be reported with the status code and the hint.
            return null;
        }
    }

    private static string? TryExtractProviderMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return Clamp(error.GetString());

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var errorMessage) &&
                    errorMessage.ValueKind == JsonValueKind.String)
                {
                    return Clamp(errorMessage.GetString());
                }
            }

            if (root.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return Clamp(message.GetString());
            }
        }
        catch (JsonException)
        {
            // Not a JSON error envelope, for example an HTML error page. The raw body is still
            // reported through CrideApiException.ResponseBody.
        }

        return null;
    }

    private static string? Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length <= MaxProviderMessageLength
            ? value
            : string.Concat(
                value.AsSpan(0, MaxProviderMessageLength),
                "... [truncated]");
    }
}
