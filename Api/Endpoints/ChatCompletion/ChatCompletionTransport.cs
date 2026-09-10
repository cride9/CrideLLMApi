using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class ChatCompletionTransport
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull
    };

    public ChatCompletionTransport(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends a request to the OpenAI API for chat completions.
    /// </summary>
    /// <param name="requestBody">The request body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> SendAsync(object requestBody, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/chat/completions");

        request.Content =
            JsonContent.Create(
                requestBody,
                options: JsonOptions);

        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }
}