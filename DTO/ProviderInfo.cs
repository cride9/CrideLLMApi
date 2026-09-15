using CrideLLMApi.Helpers;

namespace CrideLLMApi.DTO;

/// <summary>
/// Represents information about a provider, including endpoint, API key, API mode, chat options, and embedding options.
/// <para>
/// The instance passed to the <see cref="Api.CrideApi"/> constructor is copied, so the library
/// never mutates the configuration owned by the caller.
/// </para>
/// </summary>
public sealed record ProviderInfo
{
    /// <summary>
    /// Gets the base address of the OpenAI-compatible server, for example
    /// <c>new Uri("http://127.0.0.1:8080")</c>. Must be an absolute HTTP or HTTPS URI.
    /// </summary>
    public required Uri EndPoint { get; init; }

    /// <summary>
    /// Gets the optional bearer token. When it is set, the API client runs in
    /// <see cref="API_MODE.OPENAI_COMPATIBLE"/> mode and sends the Authorization header.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the chat completion configuration.
    /// </summary>
    public ChatProviderOptions Chat { get; init; } = new();

    /// <summary>
    /// Gets the embedding configuration.
    /// </summary>
    public EmbeddingProviderOptions Embeddings { get; init; } = new();

    /// <summary>
    /// Gets the extension configuration, for example the vector database to use.
    /// </summary>
    public ExtensionProviderOptions Extensions { get; init; } = new();

    /// <summary>
    /// Gets or sets the active API mode. It is derived from <see cref="ApiKey"/> by the
    /// <see cref="Api.CrideApi"/> constructor and does not have to be set by the caller.
    /// </summary>
    public API_MODE ApiMode { get; set; } = API_MODE.LOCAL;
}

/// <summary>
/// Represents options for a chat provider, including model name, streaming option, reasoning effort, tool choice, and tool execution mode.
/// </summary>
public sealed record ChatProviderOptions
{
    /// <summary>
    /// Gets the model identifier sent with every request. Required when the client runs in
    /// <see cref="API_MODE.OPENAI_COMPATIBLE"/> mode.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider streams the response. Must be
    /// <see langword="true"/>: non-streaming chat completions are not supported yet, and setting
    /// this to <see langword="false"/> fails fast with a <see cref="CrideApiException"/>.
    /// </summary>
    public bool Streaming { get; init; } = true;

    /// <summary>
    /// Gets the requested reasoning effort for providers that support it.
    /// </summary>
    public REASONING_EFFORT ReasoningEffort { get; init; } = REASONING_EFFORT.NONE;

    /// <summary>
    /// Gets the tool choice sent to the provider, for example <see cref="TOOL_CHOICE.AUTO"/>.
    /// </summary>
    public TOOL_CHOICE ToolChoice { get; init; } = TOOL_CHOICE.AUTO;

    /// <summary>
    /// Gets the requested tool execution mode.
    /// <para>
    /// Not implemented yet: the library currently always executes the tools with the
    /// OpenAI-compatible loop. This value is carried in the configuration but does not change the
    /// behavior until the asynchronous mode is implemented.
    /// </para>
    /// </summary>
    public TOOL_EXECUTION_MODE ToolExecutionMode { get; init; } = TOOL_EXECUTION_MODE.OPENAI_LOOP;

    /// <summary>
    /// The maximum number of tokens to generate. When null, the provider's
    /// default is used. Must be positive when set.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// The sampling temperature. Lower values make the output more
    /// deterministic. When null, the provider's default is used.
    /// Must not be negative when set.
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// When false, the model's reasoning/thinking mode is disabled. When null,
    /// the provider's default is used.
    /// </summary>
    public bool? EnableThinking { get; init; }
}

/// <summary>
/// Represents options for an embedding provider, including model name, target dimensions, and normalization option.
/// </summary>
public sealed record EmbeddingProviderOptions
{
    /// <summary>
    /// Gets the embedding model identifier sent with every request. Required when the client runs
    /// in <see cref="API_MODE.OPENAI_COMPATIBLE"/> mode.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets the number of dimensions the returned vectors are truncated to. When null, the full
    /// provider vector is kept. Dimensions can only be reduced, never increased, so a value
    /// larger than the provider vector size fails with a <see cref="CrideApiException"/>.
    /// </summary>
    public int? TargetDimensions { get; init; }

    /// <summary>
    /// Gets a value indicating whether the returned vectors are normalized to unit length.
    /// </summary>
    public bool Normalize { get; init; } = false;
}
