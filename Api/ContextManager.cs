using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
namespace CrideLLMApi.Api;

public class ContextManager
{
    private List<ChatMessage> _chatHistory = new();
    public static implicit operator List<ChatMessage>(ContextManager context)
    {
        return context._chatHistory;
    }

    public void Add(ChatMessage message)
    {
        _chatHistory.Add(message);
    }

    public IEnumerable<ChatMessage> GetMessages(REQUEST_ROLE role)
    {
        return _chatHistory.Where(it => it.Role == role.ToString().ToLower());
    }

    public IEnumerable<ChatMessage> GetMessages()
    {
        return _chatHistory;
    }

    public List<ChatMessage> NewMemory( )
    {
        _chatHistory.Clear( );
        return _chatHistory;
    }
    public List<ChatMessage> NewMemory(List<ChatMessage> memory)
    {
        _chatHistory.Clear( );
        _chatHistory.AddRange(memory);
        return _chatHistory;
    }
    public List<ChatMessage>? GetMemory( ) =>
        _chatHistory.Select(x => x with { }).ToList( );
}
