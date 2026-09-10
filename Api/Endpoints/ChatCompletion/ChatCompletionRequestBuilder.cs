using CrideLLMApi.Api.Tools;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class ChatCompletionRequestBuilder
{
    private readonly ProviderInfo _provider;
    private readonly ToolRegistry _tools;
    private readonly Func<ContextManager?> _contextAccessor;

    public ChatCompletionRequestBuilder(
        ProviderInfo provider,
        ToolRegistry tools,
        Func<ContextManager?> contextAccessor)
    {
        _provider = provider;
        _tools = tools;
        _contextAccessor = contextAccessor;
    }

    public ChatCompletionRequest Create(IEnumerable<ChatMessage>? messages = null)
    {
        var chatOptions = _provider.Chat;

        return new ChatCompletionRequest
        {
            Model = chatOptions.ModelName,
            Messages =
                messages ??
                _contextAccessor()?.GetMessages() ??
                [],

            Tools = _tools.Definitions,

            Stream = chatOptions.Streaming,

            ReasoningEffort =
                chatOptions.ReasoningEffort
                    .ToString()
                    .ToLower(),

            ToolChoice =
                chatOptions.ToolChoice
                    .ToString()
                    .ToLower()
        };
    }

    public ChatCompletionRequest CreateWithMessage(
        REQUEST_ROLE role,
        string content)
    {
        _contextAccessor()?.Add(
            new ChatMessage
            {
                Role = role.ToString().ToLower(),
                Content = content
            });

        return Create();
    }
}