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

    /// <summary>
    /// Creates a new ChatCompletionRequest with the specified messages or retrieves them from the context if not provided.
    /// </summary>
    /// <param name="messages">The chat messages.</param>
    /// <returns>The created chat completion request.</returns>
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

    /// <summary>
    /// Creates a new ChatCompletionRequest with a single message.
    /// </summary>
    /// <param name="role">The role of the message sender.</param>
    /// <param name="content">The message content.</param>
    /// <returns>The created chat completion request.</returns>
    public ChatCompletionRequest CreateWithMessage(REQUEST_ROLE role, string content)
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