using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
namespace CrideLLMApi.Api;

public class ContextManager
{
    /// <summary>
    /// The chat history of the conversation.
    /// </summary>
    private List<ChatMessage> _chatHistory = new();

    /// <summary>
    /// Implicitly converts a ContextManager instance to a list of chat messages.
    /// </summary>
    /// <param name="context">The context manager instance.</param>
    /// <returns>The list of chat messages.</returns>
    public static implicit operator List<ChatMessage>(ContextManager context)
    {
        return context._chatHistory;
    }

    /// <summary>
    /// Adds a chat message to the chat history.
    /// </summary>
    /// <param name="message">The chat message to add.</param>
    public void Add(ChatMessage message)
    {
        _chatHistory.Add(message);
    }

    /// <summary>
    /// Adds a chat message to the chat history.
    /// </summary>
    /// <param name="role">The role of the chat message to add.</param>
    /// <param name="content">The content of the chat message to add.</param>
    public void Add(REQUEST_ROLE role, string content)
    {
        _chatHistory.Add(new ChatMessage
        {
            Role = role.ToString().ToLower(),
            Content = content
        });
    }

    /// <summary>
    /// Adds a system message to the chat history.
    /// </summary>
    /// <param name="content">The content of the system message to add.</param>
    public void AddSystem(string content)
    {
        Add(REQUEST_ROLE.SYSTEM, content);
    }

    /// <summary>
    /// Adds a user message to the chat history.
    /// </summary>
    /// <param name="content">The content of the user message to add.</param>
    public void AddUser(string content)
    {
        Add(REQUEST_ROLE.USER, content);
    }

    /// <summary>
    /// Adds an assistant message to the chat history.
    /// </summary>
    /// <param name="content">The content of the assistant message to add.</param>
    public void AddAssistant(string content)
    {
        Add(REQUEST_ROLE.ASSISTANT, content);
    }

    /// <summary>
    /// Adds a tool message to the chat history with the specified tool call ID and content.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call.</param>
    /// <param name="content">The content of the tool message to add.</param>
    public void AddTool(
        string toolCallId,
        string content)
    {
        _chatHistory.Add(new ChatMessage
        {
            Role = REQUEST_ROLE.TOOL.ToString().ToLower(),
            ToolCallId = toolCallId,
            Content = content
        });
    }

    /// <summary>
    /// Sets the system message in the chat history, replacing any existing system messages.
    /// </summary>
    /// <param name="content">The content of the system message to set.</param>
    public void SetSystem(string content)
    {
        _chatHistory.RemoveAll(
            x => x.Role == REQUEST_ROLE.SYSTEM
                .ToString()
                .ToLower());

        _chatHistory.Insert(0, new ChatMessage
        {
            Role = REQUEST_ROLE.SYSTEM
                .ToString()
                .ToLower(),

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
        return _chatHistory.Where(it => it.Role == role.ToString().ToLower());
    }

    /// <summary>
    /// Gets all the chat messages in the chat history.
    /// </summary>
    /// <returns>The chat messages.</returns>
    public IEnumerable<ChatMessage> GetMessages()
    {
        return _chatHistory;
    }

    /// <summary>
    /// Clears the chat history and returns a new empty list of chat messages.
    /// </summary>
    /// <returns>The new empty list of chat messages.</returns>
    public List<ChatMessage> NewMemory( )
    {
        _chatHistory.Clear( );
        return _chatHistory;
    }

    /// <summary>
    /// Clears the chat history and returns a new list of chat messages initialized with the provided memory.
    /// </summary>
    /// <param name="memory">The memory to initialize the chat history with.</param>
    /// <returns>The new list of chat messages.</returns>
    public List<ChatMessage> NewMemory(List<ChatMessage> memory)
    {
        _chatHistory.Clear( );
        _chatHistory.AddRange(memory);
        return _chatHistory;
    }

    /// <summary>
    /// Gets a copy of the current chat history as a list of chat messages.
    /// </summary>
    /// <returns>A copy of the chat messages.</returns>
    public List<ChatMessage>? GetMemory( ) =>
        _chatHistory.Select(x => x with { }).ToList( );
}
