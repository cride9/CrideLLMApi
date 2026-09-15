namespace CrideLLMApi.Helpers;

/// <summary>
/// Controls how the client authenticates against the provider. The mode is derived from
/// <see cref="DTO.ProviderInfo.ApiKey"/> by the API client constructor.
/// </summary>
[Flags]
public enum API_MODE
{
    /// <summary>No API key was configured; the provider is treated as a local server.</summary>
    LOCAL = 0,

    /// <summary>An API key was configured; the Authorization header is sent with every request.</summary>
    OPENAI_COMPATIBLE = 1,

    /// <summary>The number of defined values. Not a valid mode.</summary>
    COUNT
}

/// <summary>
/// The OpenAI-compatible conversation roles.
/// </summary>
[Flags]
public enum REQUEST_ROLE
{
    /// <summary>Instructions that steer the conversation.</summary>
    SYSTEM,

    /// <summary>A message produced by the user or the calling application.</summary>
    USER,

    /// <summary>A message produced by the model.</summary>
    ASSISTANT,

    /// <summary>The result of a tool call, matched to the call through its identifier.</summary>
    TOOL,

    /// <summary>The number of defined values. Not a valid role.</summary>
    COUNT
}

/// <summary>
/// The requested reasoning effort for providers that support it.
/// </summary>
[Flags]
public enum REASONING_EFFORT
{
    /// <summary>No reasoning effort is requested.</summary>
    NONE,

    /// <summary>Low reasoning effort.</summary>
    LOW,

    /// <summary>High reasoning effort.</summary>
    HIGH,

    /// <summary>Maximum reasoning effort.</summary>
    MAX,

    /// <summary>The number of defined values. Not a valid value.</summary>
    COUNT
}

/// <summary>
/// Controls whether the model may, must, or must not call a tool.
/// </summary>
[Flags]
public enum TOOL_CHOICE
{
    /// <summary>Tools are disabled for the request.</summary>
    NONE,

    /// <summary>The model decides whether a tool is called.</summary>
    AUTO,

    /// <summary>The model must call at least one tool.</summary>
    REQUIRED,

    /// <summary>The number of defined values. Not a valid value.</summary>
    COUNT
}

/// <summary>
/// The requested tool execution mode.
/// <para>
/// Only the OpenAI-compatible loop is implemented: the value is carried in the configuration but
/// does not change the behavior until the asynchronous mode is implemented.
/// </para>
/// </summary>
[Flags]
public enum TOOL_EXECUTION_MODE
{
    /// <summary>Tool calls are executed inside the OpenAI-compatible loop.</summary>
    OPENAI_LOOP,

    /// <summary>Reserved for the asynchronous tool execution mode, which is not implemented yet.</summary>
    ASYNC,

    /// <summary>The number of defined values. Not a valid mode.</summary>
    COUNT
}

/// <summary>
/// The lifecycle status of a single tool execution.
/// </summary>
[Flags]
public enum ToolExecutionStatus
{
    /// <summary>The execution was created but has not started yet.</summary>
    QUEUED,

    /// <summary>The tool handler is running.</summary>
    RUNNING,

    /// <summary>The tool handler returned a result.</summary>
    COMPLETED,

    /// <summary>The tool handler threw an exception. See <see cref="Api.Tools.ToolExecution.Exception"/>.</summary>
    FAILED,

    /// <summary>The execution was cancelled through the cancellation token.</summary>
    CANCELLED,

    /// <summary>The number of defined values. Not a valid status.</summary>
    COUNT
}
