using CrideLLMApi.Api;
using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;

var provider = new ProviderInfo { EndPoint = new Uri("http://localhost"), Reranker = new() { ModelName = "rank", TopN = 1 } };

// No response is released until all twenty requests have reached the HTTP handler.
using (var handler = new BarrierHandler(20))
using (var client = new HttpClient(handler))
using (var api = new CrideApi(provider, client))
{
    var chat = api.GetEndpointMethods<ChatCompletion>()!;
    var embeddings = api.GetEndpointMethods<Embeddings>()!;
    var chats = Enumerable.Range(0, 10).Select(i => ReadAsync(chat, i.ToString()));
    var vectors = Enumerable.Range(0, 10).Select(async i =>
    {
        var vector = await embeddings.EmbedAsync(i.ToString());
        Check(vector.Values[0] == i, "Embedding responses were mixed.");
    });
    await Task.WhenAll(chats.Concat(vectors)).WaitAsync(TimeSpan.FromSeconds(10));
    Check(handler.Arrivals == 20, "Requests did not overlap.");
    chat.AddContextManager(new ContextManager());
}

using (var handler = new BarrierHandler(10))
using (var client = new HttpClient(handler))
using (var api = new CrideApi(provider, client))
{
    await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
    {
        var chat = api.CreateEndpointMethods<ChatCompletion>()!;
        var context = new ContextManager();
        chat.AddContextManager(context);
        await ReadAsync(chat, i.ToString());
        Check(context.GetMessages().Count() == 1, "Conversation histories were mixed.");
    })).WaitAsync(TimeSpan.FromSeconds(10));
}

// A pending call must retain its configuration guard even if another call is cancelled.
using (var handler = new BarrierHandler(3))
using (var client = new HttpClient(handler))
using (var api = new CrideApi(provider, client))
{
    var chat = api.GetEndpointMethods<ChatCompletion>()!;
    using var cancellation = new CancellationTokenSource();
    var first = ReadAsync(chat, "0", cancellation.Token);
    var second = ReadAsync(chat, "1");
    cancellation.Cancel();
    await ThrowsAsync<OperationCanceledException>(() => first);
    await ThrowsAsync<InvalidOperationException>(() =>
    {
        chat.AddContextManager(new ContextManager());
        return Task.CompletedTask;
    });
    await Task.WhenAll(second, ReadAsync(chat, "2"));
    chat.AddContextManager(new ContextManager());
}

using (var handler = new BarrierHandler(2))
using (var client = new HttpClient(handler))
using (var api = new CrideApi(provider, client))
{
    var chat = api.GetEndpointMethods<ChatCompletion>()!;
    var context = new ContextManager();
    chat.AddContextManager(context);
    using var cancellation = new CancellationTokenSource();
    var first = ReadAsync(chat, "0", cancellation.Token);
    await ThrowsAsync<InvalidOperationException>(() => ReadAsync(chat, "1"));
    Check(context.GetMessages().Count() == 1, "Rejected call changed history.");
    cancellation.Cancel();
    await ThrowsAsync<OperationCanceledException>(() => first);
    await ReadAsync(chat, "2");
}

using (var handler = new BarrierHandler(10))
using (var client = new HttpClient(handler))
using (var api = new CrideApi(provider with { Chat = new() { Streaming = false } }, client))
{
    // Endpoint lookup must not instantiate an unrelated, incompatible chat endpoint.
    Check(api.GetEndpointMethods<Embeddings>() is not null, "Embedding-only lookup failed.");
    var reranker = api.GetEndpointMethods<Reranker>()!;
    Check(ReferenceEquals(reranker, api.GetEndpointMethods<Reranker>()), "Endpoint was not cached.");
    await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
    {
        var result = await reranker.RerankAsync("question", ["first", "second"]);
        Check(result.Count == 1 && result[0].Index == 1 && result[0].RelevanceScore == 0.9, "Invalid ranking.");
    })).WaitAsync(TimeSpan.FromSeconds(10));
    await ThrowsAsync<ArgumentException>(() => reranker.CreateAsync("", ["text"]));
    await ThrowsAsync<ArgumentException>(() => reranker.CreateAsync("query", []));
    await ThrowsAsync<ArgumentOutOfRangeException>(() => reranker.CreateAsync(reranker.CreateRequest("query", ["text"]) with { TopN = 0 }));
}

foreach (var body in new[] { "null", "{}", "{\"results\":[{}]}", "{\"results\":[{\"index\":9,\"relevance_score\":0.2}]}" })
{
    using var client = new HttpClient(new ResponseHandler(HttpStatusCode.OK, body));
    using var api = new CrideApi(provider, client);
    await ThrowsAsync<CrideApiException>(() => api.GetEndpointMethods<Reranker>()!.CreateAsync("query", ["text"]));
}
using (var client = new HttpClient(new ResponseHandler(HttpStatusCode.Unauthorized, "denied")))
using (var api = new CrideApi(provider, client))
{
    try { await api.GetEndpointMethods<Reranker>()!.CreateAsync("query", ["text"]); throw new Exception("Expected failure."); }
    catch (CrideApiException exception) { Check(exception.StatusCode == HttpStatusCode.Unauthorized, "Status code lost."); }
}

Console.WriteLine("Passed: 10 chat + 10 embedding requests concurrently, conversation isolation, cancellation, context guard, 10 rerank requests, schema validation and HTTP errors.");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

static async Task ReadAsync(ChatCompletion chat, string message, CancellationToken cancellationToken = default)
{
    var result = new StringBuilder();
    await foreach (var delta in chat.GetResponseAsync(message, cancellationToken))
        result.Append(delta.Content);
    Check(result.ToString() == message, "Chat responses were mixed.");
}

sealed class BarrierHandler(int count) : HttpMessageHandler
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;
    public int Arrivals => _arrivals;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (Interlocked.Increment(ref _arrivals) >= count) _ready.TrySetResult();
        await _ready.Task.WaitAsync(cancellationToken);
        var root = json.RootElement;
        string body;
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/v1/chat/completions")
        {
            var content = root.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString();
            body = "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content } } } }) + "\n\ndata: [DONE]\n\n";
        }
        else if (path == "/v1/embeddings")
            body = "{\"data\":[{\"index\":0,\"embedding\":[" + root.GetProperty("input").GetString() + ",1]}]}";
        else if (path == "/v1/rerank")
        {
            if (root.GetProperty("model").GetString() != "rank" || root.GetProperty("top_n").GetInt32() != 1 || root.GetProperty("documents").GetArrayLength() != 2)
                throw new Exception("Invalid reranking payload.");
            body = "{\"results\":[{\"index\":1,\"relevance_score\":0.9}]}";
        }
        else throw new Exception("Unexpected endpoint: " + path);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}

sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
}
