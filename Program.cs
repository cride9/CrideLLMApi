using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Text.Json;

CrideApi _api = new(new ProviderInfo()
{
    EndPoint = new Uri("http://127.0.0.1:8080"),
    Streaming = true,
    ReasoningEffort = REASONING_EFFORT.NONE,
    ToolChoice = TOOL_CHOICE.AUTO
});

ApiFunction function = new()
{
    Name = "hello_user",
    Description = "A simple function that returns a greeting message.",
    Properties =
    {
        ["name"] = new FunctionProperties
        {
            Type = "string", 
            Description = "The name of the person to greet."
        }
    },
    Required = new[] { "name" }
};

_api.ToolCallExecuted += (tool, result) =>
{
    Console.WriteLine($"[tool] {tool.Function?.Name}({tool.Function?.Arguments}) => {result}");
};

Task<string> HelloUser(string args)
{
    var doc = JsonDocument.Parse(args);
    var name = doc.RootElement.GetProperty("name").GetString();
    return Task.FromResult($"Hello, {name}!");
}

_api.AddFunction(function, HelloUser);

await foreach (var item in _api.GetResponseAsync("Greet Cride"))
{
    if (item.Role == REQUEST_ROLE.ASSISTANT.ToString().ToLower())
        Console.Write(item);
}
Console.ReadKey();