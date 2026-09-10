using CrideLLMApi.Api;
using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

/// <summary>
/// This is an example of how to use the CrideLLMApi.
/// CHANGE PROPERTIES TO CONSOLE APP TO RUN THIS EXAMPLE IF IT DOESNT START BY PRESSING F5.
/// </summary>
class Example
{
    // Api creation example
    static CrideApi _api = new(new ProviderInfo()
    {
        EndPoint = new Uri("http://127.0.0.1:8080"),
        Streaming = true,
        ReasoningEffort = REASONING_EFFORT.NONE,
        ToolChoice = TOOL_CHOICE.AUTO,
        ToolExecutionMode = TOOL_EXECUTION_MODE.ASYNC,
        EmbeddingModelName = "Qwen.Qwen3-VL-Embedding-2B.Q4_K_S",
        ModelName = "Qwen3.5-2B-UD-Q4_K_XL"
    });

    // ContextManager creation example
    static ContextManager ctxManager = new( );

    static async Task Main(string[] args)
    {
        await EmbeddingExample(_api);
        await ChatCompletionExample(_api, ctxManager);

        Console.ReadKey( );
    }

    /// <summary>
    /// This is an example of how to use the CrideLLMApi embedding endpoint
    /// </summary>
    public static async Task EmbeddingExample(CrideApi _api)
    {
        // Get endpoint functions for Embeddings
        var embedding = _api.GetEndpointMethods<Embeddings>()!;

        // Embedding example
        var embeddedText = (await embedding.EmbedAsync("Hello world!")).Normalize();
        var otherText = (await embedding.EmbedAsync("Hello world! This should be close in similarity!")).Normalize();

        // Truncate and normalize the embedding vector with the Extension if needed
        var truncatedEmbedding1 = embeddedText.Truncate(1024).Normalize();

        // Calculate the cosine similarity between two embeddings
        var similarity = embeddedText.CosineSimilarity(otherText);

        Console.WriteLine($"Cosine Similarity: {similarity} (Between: \"Hello world!\" and \"Hello world! This should be close in similarity!\")");
    }

    /// <summary>
    /// This is an example of how to use the CrideLLMApi chat completion endpoint
    /// </summary>
    public static async Task ChatCompletionExample(CrideApi _api, ContextManager ctxManager)
    {
        // Get endpoint functions for ChatCompletion
        var chatCompletion = _api.GetEndpointMethods<ChatCompletion>()!;

        // Context example
        chatCompletion.AddContextManager(ctxManager);

        // Image input example
        ctxManager.AddUserImage("What is on this picture?", "https://media.newyorker.com/photos/59095bb86552fa0be682d9d0/master/w_2560%2Cc_limit/Monkey-Selfie.jpg");

        // Functioncall registration/calling example
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

        // Getting raw response example
        await foreach (var item in chatCompletion.GetRawResponseAsync("Hows the weather in Budapest? And also what is on the image I sent you?"))
        {
            Console.Write(item.Choices[0].Delta.Content);
        }
    }

    // Placeholder for the function that will be called by the model
    public static async Task<string> GetCurrentWeather(GetWeatherArgs args)
    {
        Console.WriteLine($"[TOOL CALL] The current weather in {args.name} is 25 degrees {args.unit}.");
        return $"The current weather in {args.name} is 25 degrees {args.unit}.";
    }
}

// Placeholder args class for the GetCurrentWeather function
class GetWeatherArgs
{
    public string name { get; set; }
    public string unit { get; set; }
}