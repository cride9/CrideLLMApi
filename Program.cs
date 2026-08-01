using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

CrideApi _api = new(new ProviderInfo()
{
    EndPoint = new Uri("http://127.0.0.1:8080"),
    Streaming = true,
    ReasoningEffort = REASONING_EFFORT.NONE,
    ToolChoice = TOOL_CHOICE.AUTO
});
// Creates a new conversation memory for the API. This is useful if you want to start a new conversation without any previous context.
_api.NewMemory();

await foreach (var item in _api.GetResponseAsync("Hi! My name is Cride :D"))
{
    Console.Write(item.Content);
}
Console.WriteLine();

await foreach (var item in _api.GetResponseAsync("What was my name?"))
{
    Console.Write(item.Content);
}
Console.WriteLine();

var memoryBefore = _api.GetMemory();
_api.NewMemory(); // Fresh start
await foreach (var item in _api.GetResponseAsync("What was my name?"))
{
    Console.Write(item.Content);
}
Console.WriteLine();

_api.NewMemory(memoryBefore); // Restore previous memory
await foreach (var item in _api.GetResponseAsync("What was my name?"))
{
    Console.Write(item.Content);
}
Console.WriteLine();

Console.ReadKey();