using CrideLLMApi.Api;
using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

class Example
{
    public async Task Main(string[ ] args)
    {
        CrideApi _api = new(new ProviderInfo( )
        {
            EndPoint = new Uri("http://127.0.0.1:8080"),
            Streaming = true,
            ReasoningEffort = REASONING_EFFORT.NONE,
            ToolChoice = TOOL_CHOICE.AUTO
        });

        var chatCompletion = _api.GetEndpointMethods<ChatCompletion>( );
        if ( chatCompletion is null )
            return;

        ContextManager ctxManager = new( );
        chatCompletion.AddContextManager(ctxManager);

        await foreach ( var item in chatCompletion.GetResponseAsync("Hi! My name is Cride :D") )
        {
            Console.Write(item.Content);
        }

        Console.ReadKey( );
    }
}

