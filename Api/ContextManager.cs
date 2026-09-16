using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
namespace CrideLLMApi.Api;

/// <summary>
/// Manages the conversation history that is sent to the provider with every request.
/// Keep a separate instance per conversation. Concurrent conversation generations sharing
/// one context are rejected; direct history edits remain caller-controlled and must not race
/// with request construction or tool-result writes.
/// <para>
/// All methods that return the history return the live internal list, so the returned value must
/// not be modified by the caller. Use <see cref="GetMemory"/> when a defensive copy is needed.
/// </para>
/// </summary>
public class ContextManager
{
    private int _generationActive;

    internal void BeginGeneration()
    {
        if (Interlocked.CompareExchange(ref _generationActive, 1, 0) != 0)
            throw new InvalidOperationException(
                "This context is already used by an active conversation generation. Wait for it to finish or use a separate ContextManager.");
    }

    internal void EndGeneration() => Volatile.Write(ref _generationActive, 0);

    /// <summary>
    /// The chat history of the conversation.
    /// </summary>
    private List<ChatMessage> _chatHistory = new();

    /// <summary>
    /// Implicitly converts a ContextManager instance to a list of chat messages.
    /// </summary>
    /// <param name="context">The context manager instance.</param>
    /// <returns>The live list of chat messages. Do not mutate it.</returns>
    public static implicit operator List<ChatMessage>(ContextManager context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context._chatHistory;
    }

    /// <summary>
    /// Adds a chat message to the chat history.
    /// </summary>
    /// <param name="message">The chat message to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public void Add(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _chatHistory.Add(message);
    }

    /// <summary>
    /// Adds a chat message to the chat history.
    /// </summary>
    /// <param name="role">The role of the chat message to add.</param>
    /// <param name="content">The content of the chat message to add.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty or whitespace.</exception>
    public void Add(REQUEST_ROLE role, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _chatHistory.Add(new ChatMessage
        {
            Role = ToRoleName(role),
            Content = content
        });
    }

    /// <summary>
    /// Adds a user message to the chat history with the specified text and content parts.
    /// </summary>
    /// <param name="text">The text of the user message to add.</param>
    /// <param name="contentParts">The content parts of the user message to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentParts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <paramref name="text"/> nor any content part provides content, or when
    /// a content part is null.
    /// </exception>
    public void AddUser(string text, params ChatContentPart[] contentParts)
    {
        ArgumentNullException.ThrowIfNull(contentParts);

        var content = new List<ChatContentPart>();

        if (!string.IsNullOrWhiteSpace(text))
        {
            content.Add(new TextContentPart
            {
                Text = text
            });
        }

        foreach (var contentPart in contentParts)
        {
            if (contentPart is null)
            {
                throw new ArgumentException(
                    "User message content parts cannot contain null entries.",
                    nameof(contentParts));
            }

            content.Add(contentPart);
        }

        if (content.Count == 0)
        {
            throw new ArgumentException(
                "A user message must contain text or at least one content part.",
                nameof(contentParts));
        }

        _chatHistory.Add(new ChatMessage
        {
            Role = ToRoleName(REQUEST_ROLE.USER),

            Content = content
        });
    }

    /// <summary>
    /// Adds a user message to the chat history with the specified text and an image file.
    /// </summary>
    /// <param name="text">The text of the user message to add.</param>
    /// <param name="filePath">The path to the image file, or the URL of the image.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null, empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when a local image file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when a local image file has an unsupported format.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when a local image file cannot be read, or when a remote reference is not a usable
    /// image URL.
    /// </exception>
    public void AddUserImage(string text, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp ||
         uri.Scheme == Uri.UriSchemeHttps))
        {
            AddUser(text, ImageContent.FromUrl(filePath));
        }
        else
        {
            AddUser(text, ImageContent.FromFile(filePath));
        }
    }

    /// <summary>
    /// Adds a system message to the chat history.
    /// </summary>
    /// <param name="content">The content of the system message to add.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty or whitespace.</exception>
    public void AddSystem(string content)
    {
        Add(REQUEST_ROLE.SYSTEM, content);
    }

    /// <summary>
    /// Adds a user message to the chat history.
    /// </summary>
    /// <param name="content">The content of the user message to add.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty or whitespace.</exception>
    public void AddUser(string content)
    {
        Add(REQUEST_ROLE.USER, content);
    }

    /// <summary>
    /// Adds an assistant message to the chat history.
    /// </summary>
    /// <param name="content">The content of the assistant message to add.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty or whitespace.</exception>
    public void AddAssistant(string content)
    {
        Add(REQUEST_ROLE.ASSISTANT, content);
    }

    /// <summary>
    /// Adds a tool message to the chat history with the specified tool call ID and content.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call.</param>
    /// <param name="content">The content of the tool message to add.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="toolCallId"/> is null, empty or whitespace, or when
    /// <paramref name="content"/> is null.
    /// </exception>
    public void AddTool(
        string toolCallId,
        string content)
    {
        // The provider matches a tool result to its call through this identifier, so an empty
        // value would produce an unusable request.
        ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
        ArgumentNullException.ThrowIfNull(content);

        _chatHistory.Add(new ChatMessage
        {
            Role = ToRoleName(REQUEST_ROLE.TOOL),
            ToolCallId = toolCallId,
            Content = content
        });
    }

    /// <summary>
    /// Sets the system message in the chat history, replacing any existing system messages.
    /// </summary>
    /// <param name="content">The content of the system message to set.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty or whitespace.</exception>
    public void SetSystem(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _chatHistory.RemoveAll(
            x => x.Role == ToRoleName(REQUEST_ROLE.SYSTEM));

        _chatHistory.Insert(0, new ChatMessage
        {
            Role = ToRoleName(REQUEST_ROLE.SYSTEM),

            Content = content
        });
    }

    /// <summary>
    /// Gets the chat messages filtered by the specified role.
    /// </summary>
    /// <param name="role">The role to filter by.</param>
    /// <returns>The filtered chat messages.</returns>
    public IEnumerable<ChatMessage> GetMessages(REQUEST_ROLE role)
    {
        var roleName = ToRoleName(role);

        return _chatHistory.Where(it => it.Role == roleName);
    }

    /// <summary>
    /// Gets all the chat messages in the chat history.
    /// </summary>
    /// <returns>The live chat messages. Do not mutate the sequence.</returns>
    public IEnumerable<ChatMessage> GetMessages()
    {
        return _chatHistory;
    }

    /// <summary>
    /// Clears the chat history and returns the cleared list of chat messages.
    /// </summary>
    /// <returns>The live internal list, which is empty after the call.</returns>
    public List<ChatMessage> NewMemory( )
    {
        _chatHistory.Clear( );
        return _chatHistory;
    }

    /// <summary>
    /// Replaces the chat history with the provided memory.
    /// </summary>
    /// <param name="memory">
    /// The memory to initialize the chat history with. The sequence is materialized before the
    /// history is cleared, so passing a lazy sequence over the current history is safe.
    /// </param>
    /// <returns>The live internal list containing the provided memory.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="memory"/> is null.</exception>
    public List<ChatMessage> NewMemory(IEnumerable<ChatMessage> memory)
    {
        ArgumentNullException.ThrowIfNull(memory);

        // Replacing the memory with itself must be a no-op: clearing first and re-adding the same
        // list would silently wipe the conversation.
        if (ReferenceEquals(memory, _chatHistory))
            return _chatHistory;

        var replacement = memory.ToArray( );

        _chatHistory.Clear( );
        _chatHistory.AddRange(replacement);

        return _chatHistory;
    }

    /// <summary>
    /// Gets a copy of the current chat history as a list of chat messages.
    /// </summary>
    /// <returns>A copy of the chat messages.</returns>
    public List<ChatMessage>? GetMemory( ) =>
        _chatHistory.Select(x => x with { }).ToList( );

    /// <summary>
    /// Converts a role to the lowercase role name expected by the OpenAI-compatible API.
    /// The invariant culture is used so the request does not depend on the current culture.
    /// </summary>
    /// <param name="role">The role to convert.</param>
    /// <returns>The role name.</returns>
    private static string ToRoleName(REQUEST_ROLE role) =>
        role.ToString( ).ToLowerInvariant( );
}
