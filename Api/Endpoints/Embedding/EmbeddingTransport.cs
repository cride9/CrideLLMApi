using CrideLLMApi.DTO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class EmbeddingTransport
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull
    };

    public EmbeddingTransport(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends an embedding request to the API and returns the HTTP response message.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> SendAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/embeddings")
        {
            Content = JsonContent.Create(
                request,
                options: JsonOptions)
        };

        return await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }
}
