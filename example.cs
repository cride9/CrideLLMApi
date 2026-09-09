using CrideLLMApi.Api;
using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

/// <summary>
/// This is an example of how to use the CrideLLMApi to get the current weather in a given location.
/// CHANGE PROPERTIES TO CONSOLE APP TO RUN THIS EXAMPLE
/// </summary>
class Example
{
    static async Task Main(string[] args)
    {
        CrideApi _api = new(new ProviderInfo( )
        {
            EndPoint = new Uri("http://127.0.0.1:8080"),
            Streaming = true,
            ReasoningEffort = REASONING_EFFORT.NONE,
            ToolChoice = TOOL_CHOICE.AUTO,
            ToolExecutionMode = TOOL_EXECUTION_MODE.ASYNC
        });

        var chatCompletion = _api.GetEndpointMethods<ChatCompletion>( )!;

        ContextManager ctxManager = new( );
        chatCompletion.AddContextManager(ctxManager);

        ctxManager.AddUserImage("What is on this picture?", "https://media.newyorker.com/photos/59095bb86552fa0be682d9d0/master/w_2560%2Cc_limit/Monkey-Selfie.jpg");

        chatCompletion.AddFunction<GetWeatherArgs>(new()
        {
            Name = "get_current_weather",
            Description = "Get the current weather in a given location",
            Properties = new()
            {
                ["name"] = new() { Type = "string", Description = "The name of the location" },
                ["unit"] = new() { Type = "string", Description = "The unit of temperature (celsius or fahrenheit)" }
            },
            Required = ["name", "unit"]
        }, GetCurrentWeather);

        await foreach ( var item in chatCompletion.GetRawResponseAsync("Hows the weather in Budapest? And also what is on the image I sent you?") )
        {
            Console.Write(item.Choices[0].Delta.Content);
        }

        Console.ReadKey( );
    }

    public static async Task<string> GetCurrentWeather(GetWeatherArgs args)
    {
        Console.WriteLine($"[TOOL CALL] The current weather in {args.name} is 25 degrees {args.unit}.");
        return $"The current weather in {args.name} is 25 degrees {args.unit}.";
    }
}

class GetWeatherArgs
{
    public string name { get; set; }
    public string unit { get; set; }
}