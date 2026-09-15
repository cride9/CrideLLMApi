using CrideLLMApi.Api.Tools;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.Api.Endpoints;

internal sealed class ChatCompletionRequestBuilder
{
    private const string ComponentName = "ChatCompletionRequestBuilder";

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
    /// <exception cref="CrideApiException">
    /// Thrown when the resolved message list is empty or when the configured provider mode
    /// requires a model name that is not set.
    /// </exception>
    public ChatCompletionRequest Create(IEnumerable<ChatMessage>? messages = null)
    {
        var chatOptions = _provider.Chat;

        ValidateModelName(chatOptions.ModelName);
        ValidateSamplingOptions(chatOptions);

        // Materialized so an expensive or single-use enumeration is not walked twice and so an
        // empty request is detected before it reaches the provider.
        var resolvedMessages = (messages ?? _contextAccessor()?.GetMessages() ?? []).ToArray();

        if (resolvedMessages.Length == 0)
        {
            throw new CrideApiException(
                message:
                    "The chat completion request contains no messages.",
                component: ComponentName,
                operation: "Build the chat completion request",
                hint:
                    "Add at least one message through a ContextManager (AddUser, AddSystem, ...) or pass an explicit message collection to CreateRequest.");
        }

        var tools = _tools.Definitions.ToArray();

        return new ChatCompletionRequest
        {
            Model = chatOptions.ModelName,

            Messages = resolvedMessages,

            // An empty tool array is sent as absent so providers that reject "tools": [] accept
            // the request.
            Tools = tools.Length == 0
                ? null
                : tools,

            Stream = chatOptions.Streaming,

            // Only sent when an effort was actually requested: "none" is not a value the
            // OpenAI-compatible servers accept, and omitting the field keeps the provider default.
            ReasoningEffort =
                chatOptions.ReasoningEffort == REASONING_EFFORT.NONE
                    ? null
                    : chatOptions.ReasoningEffort
                        .ToString()
                        .ToLowerInvariant(),

            // Only sent when tools are supplied: providers that validate the pair reject a tool
            // choice that arrives without tool definitions.
            ToolChoice =
                tools.Length == 0
                    ? null
                    : chatOptions.ToolChoice
                        .ToString()
                        .ToLowerInvariant(),

            MaxTokens = chatOptions.MaxTokens,

            Temperature = chatOptions.Temperature,

            ChatTemplateKwargs =
                chatOptions.EnableThinking is bool enableThinking
                    ? new ChatTemplateOptions { EnableThinking = enableThinking }
                    : null
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
        var message = new ChatMessage
        {
            Role = role.ToString().ToLowerInvariant(),
            Content = content
        };

        var context = _contextAccessor();

        // Without a ContextManager there is no history to build the request from, so the message
        // is sent on its own instead of being silently dropped.
        if (context is null)
            return Create([message]);

        context.Add(message);

        return Create();
    }

    private void ValidateModelName(string? modelName)
    {
        if (!_provider.ApiMode.HasFlag(API_MODE.OPENAI_COMPATIBLE))
            return;

        if (!string.IsNullOrWhiteSpace(modelName))
            return;

        throw new CrideApiException(
            message:
                "ProviderInfo.Chat.ModelName is not set, but the OpenAI-compatible provider mode requires an explicit model name.",
            component: ComponentName,
            operation: "Build the chat completion request",
            hint:
                "Set ProviderInfo.Chat.ModelName to the identifier of the model served by ProviderInfo.EndPoint.");
    }

    private static void ValidateSamplingOptions(ChatProviderOptions chatOptions)
    {
        if (chatOptions.MaxTokens is int maxTokens && maxTokens <= 0)
        {
            throw new CrideApiException(
                message:
                    $"ProviderInfo.Chat.MaxTokens is {maxTokens}, which is not a valid token budget.",
                component: ComponentName,
                operation: "Build the chat completion request",
                hint:
                    "Set ProviderInfo.Chat.MaxTokens to a positive value or leave it null to use the provider default.");
        }

        if (chatOptions.Temperature is double temperature && temperature < 0)
        {
            throw new CrideApiException(
                message:
                    $"ProviderInfo.Chat.Temperature is {temperature}, which is not a valid sampling temperature.",
                component: ComponentName,
                operation: "Build the chat completion request",
                hint:
                    "Set ProviderInfo.Chat.Temperature to zero or a positive value, or leave it null to use the provider default.");
        }
    }
}
