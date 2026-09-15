using System.Net;
using System.Text;

namespace CrideLLMApi.Helpers;

/// <summary>
/// Represents an error raised by CrideLLMApi that the library consumer can usually act on.
/// <para>
/// This exception is used for provider and configuration failures, invalid API usage,
/// HTTP/API failures and provider payloads that cannot be processed. Plain caller mistakes
/// keep the standard argument exceptions, and cancellation keeps
/// <see cref="OperationCanceledException"/>.
/// </para>
/// <para>
/// The message always contains the affected component, the failed operation, what went wrong
/// and - when the problem can be fixed from the outside - what to change. The inner exception
/// carries the original failure when there is one.
/// </para>
/// </summary>
public sealed class CrideApiException : Exception
{
    private const int MaxResponseBodyLength = 2048;

    /// <summary>
    /// Gets the HTTP status code of the failed API request, or <see langword="null"/> when the
    /// error was not caused by an HTTP response (for example a configuration problem).
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the provider response body, truncated to a readable length, or
    /// <see langword="null"/> when no body was available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Gets the library component that raised the error, for example <c>ChatCompletion</c>,
    /// <c>Embeddings</c> or <c>VectorDatabaseRegistry</c>.
    /// </summary>
    public string? Component { get; }

    /// <summary>
    /// Gets the operation that failed, for example
    /// <c>POST 'v1/chat/completions' to 'http://127.0.0.1:8080/'</c>.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Gets a short description of what the consumer can change to fix the error, when the
    /// error is fixable from the outside.
    /// </summary>
    public string? Hint { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrideApiException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CrideApiException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrideApiException"/> class with a message
    /// and the exception that caused it.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The original exception.</param>
    public CrideApiException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrideApiException"/> class with full error
    /// context. The final message is composed from the supplied parts so every error reports
    /// the component, the operation, the problem, the optional hint and the provider response.
    /// </summary>
    /// <param name="message">The description of what went wrong.</param>
    /// <param name="component">The library component that raised the error.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="innerException">The original exception, when there is one.</param>
    /// <param name="statusCode">The HTTP status code, when the error came from a response.</param>
    /// <param name="responseBody">The provider response body, when there is one.</param>
    /// <param name="hint">What the consumer can change to fix the error.</param>
    public CrideApiException(
        string message,
        string? component = null,
        string? operation = null,
        Exception? innerException = null,
        HttpStatusCode? statusCode = null,
        string? responseBody = null,
        string? hint = null)
        : base(
            Compose(message, component, operation, hint, responseBody),
            innerException)
    {
        Component = component;
        Operation = operation;
        StatusCode = statusCode;
        ResponseBody = Truncate(responseBody);
        Hint = hint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrideApiException"/> class for a failed API
    /// request. Prefer the factory used by the internal transports, which also fills in the
    /// component, the operation and an actionable hint.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the provider.</param>
    /// <param name="responseBody">The provider response body.</param>
    public CrideApiException(HttpStatusCode statusCode, string? responseBody)
        : base($"API request failed with {(int)statusCode} ({statusCode}). Response: {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = Truncate(responseBody);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrideApiException"/> class for a failed API
    /// request, keeping the component, the operation and the response body.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the provider.</param>
    /// <param name="responseBody">The provider response body.</param>
    /// <param name="component">The library component that raised the error.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="hint">What the consumer can change to fix the error.</param>
    /// <param name="innerException">The original exception, when there is one.</param>
    public CrideApiException(
        HttpStatusCode statusCode,
        string? responseBody,
        string? component,
        string? operation,
        string? hint = null,
        Exception? innerException = null)
        : base(
            Compose(
                $"The provider failed the request with HTTP {(int)statusCode} ({statusCode}).",
                component,
                operation,
                hint,
                responseBody),
            innerException)
    {
        StatusCode = statusCode;
        Component = component;
        Operation = operation;
        ResponseBody = Truncate(responseBody);
        Hint = hint;
    }

    private static string Compose(
        string message,
        string? component,
        string? operation,
        string? hint,
        string? responseBody)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(component))
            builder.Append('[').Append(component).Append("] ");

        if (!string.IsNullOrWhiteSpace(operation))
            builder.Append(operation).Append(": ");

        builder.Append(message.Trim());

        if (!string.IsNullOrWhiteSpace(hint))
            builder.AppendLine().Append("Hint: ").Append(hint.Trim());

        var body = Truncate(responseBody);

        if (body is not null)
            builder.AppendLine().Append("Provider response: ").Append(body);

        return builder.ToString();
    }

    private static string? Truncate(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        return responseBody.Length <= MaxResponseBodyLength
            ? responseBody
            : string.Concat(
                responseBody.AsSpan(0, MaxResponseBodyLength),
                "... [truncated, ",
                responseBody.Length.ToString(),
                " characters total]");
    }
}
