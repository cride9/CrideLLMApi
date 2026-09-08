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
