# CrideLLMApi

A lightweight, streaming-first .NET client library for interacting with OpenAI-compatible LLM APIs. Built for .NET 10 with first-class support for tool/function calling, streaming responses, and reasoning effort control.

## Why CrideLLMApi?

Most high-level LLM client libraries abstract away the raw API responses, hiding tool call streaming deltas, finish reasons, and other low-level details. **CrideLLMApi** gives you full access to every chunk the server sends back — including streamed tool call fragments — via [`GetRawResponseAsync`](#getrawresponseasyncstring-message). Use the high-level [`GetResponseAsync`](#getresponseasyncstring-message) when you just want text, or drop down to raw chunks when you need complete control.

Tool calling is handled through a simple **delegate** (`Func<string, Task<string>>`) rather than forcing you to implement an interface. Just register a lambda or method — no boilerplate, no ceremony.

> **Bottom line:** no black boxes. Every SSE event, every tool call delta, every finish reason is yours to inspect.

## Features

- **Streaming by default** — consume responses as `IAsyncEnumerable<Delta>` for real-time token output
- **Tool/function calling** — define functions with JSON Schema properties, register handlers, and let the LLM invoke them automatically in a conversation loop
- **Dual API mode** — works with both local (no API key) and OpenAI-compatible remote endpoints
- **Reasoning effort control** — configure `NONE`, `LOW`, `HIGH`, or `MAX` reasoning effort for supported models
- **Tool choice modes** — `AUTO` (model decides) or `REQUIRED` (force tool use)
- **SSE streaming** — parses Server-Sent Events from the chat completions endpoint
- **Event-driven tool execution** — subscribe to `ToolCallExecuted` to observe or log tool invocations
- **Minimal dependencies** — only the .NET base class libraries (`System.Net.Http`, `System.Text.Json`)

## Installation

Clone the repository and add a project reference, or copy the source into your solution.

```xml
<ProjectReference Include="..\CrideLLMApi\CrideLLMApi.csproj" />
```

> NuGet packaging may be added in the future.

## Quick Start

```csharp
using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

// Connect to a local LLM server (e.g., LM Studio, Ollama, llama.cpp server)
var api = new CrideApi(new ProviderInfo
{
    EndPoint = new Uri("http://127.0.0.1:8080"),
    Streaming = true,
    ReasoningEffort = REASONING_EFFORT.NONE,
    ToolChoice = TOOL_CHOICE.AUTO
});

// Stream the response token by token
await foreach (var delta in api.GetResponseAsync("Explain quantum computing in one sentence."))
{
    if (delta.Role == "assistant")
        Console.Write(delta.Content);
}
```

### With an OpenAI-compatible API key

```csharp
var api = new CrideApi(new ProviderInfo
{
    EndPoint = new Uri("https://api.openai.com"),
    ApiKey = "sk-...",
    ModelName = "gpt-4o",
    Streaming = true
});
```

When an `ApiKey` is provided, the library automatically switches to `OPENAI_COMPATIBLE` mode and sends a `Bearer` authorization header.

## API Reference

### Constructors

`CrideApi` provides three constructor overloads:

| Signature | Description |
|---|---|
| `CrideApi(ProviderInfo)` | Full configuration via a [`ProviderInfo`](#providerinfo) record |
| `CrideApi(Uri endpoint, string? apiKey, string? modelName, bool streaming)` | Convenience overload with a `Uri` |
| `CrideApi(string endpoint, string? apiKey, string? modelName, bool streaming)` | Convenience overload with a string URL |

### Methods

#### `GetResponseAsync(string message)`

Returns `IAsyncEnumerable<Delta>` — streams only content-bearing deltas from the assistant. This is the primary method for simple chat interactions.

```csharp
await foreach (var delta in api.GetResponseAsync("Hello!"))
    Console.Write(delta.Content);
```

#### `GetRawResponseAsync(string message)`

Returns `IAsyncEnumerable<ChatCompletionChunk>` — streams every SSE chunk including metadata (`id`, `model`, `finish_reason`, etc.). Useful when you need access to raw chunk data.

#### `AddFunction(ApiFunction function, ToolExecutor? handler)`

Registers a tool/function that the LLM can call. See [Tool Calling](#tool-calling) below.

#### `AddFunctions(IEnumerable<(ApiFunction, ToolExecutor?)> functions)`

Batch-registers multiple functions at once.

#### `GetFunctionCalls()`

Returns the current list of registered functions as `IEnumerable<FunctionCallObject>`.

### Events

#### `ToolCallExecuted`

```csharp
api.ToolCallExecuted += (tool, result) =>
{
    Console.WriteLine($"[tool] {tool.Function?.Name}({tool.Function?.Arguments}) => {result}");
};
```

Fires each time a tool is invoked. Receives the `ToolCall` metadata and the string result.

### `ProviderInfo`

| Field | Type | Default | Description |
|---|---|---|---|
| `EndPoint` | `Uri` | **required** | Base URL of the LLM API server |
| `ApiKey` | `string?` | `null` | API key for OpenAI-compatible endpoints |
| `ModelName` | `string?` | `null` | Model name sent in the request body |
| `Streaming` | `bool` | `true` | Enable/disable SSE streaming |
| `ApiMode` | `API_MODE` | auto-detected | `LOCAL` (no key) or `OPENAI_COMPATIBLE` (key present) |
| `ReasoningEffort` | `REASONING_EFFORT` | `NONE` | Reasoning effort level |
| `ToolChoice` | `TOOL_CHOICE` | `AUTO` | Tool calling strategy |

### Enums

| Enum | Values | Description |
|---|---|---|
| `API_MODE` | `LOCAL`, `OPENAI_COMPATIBLE` | Determines auth header behavior |
| `REQUEST_ROLE` | `SYSTEM`, `USER`, `ASSISTANT`, `TOOL` | Chat message roles |
| `REASONING_EFFORT` | `NONE`, `LOW`, `HIGH`, `MAX` | Reasoning effort for o-series models |
| `TOOL_CHOICE` | `AUTO`, `REQUIRED` | Whether the model must call a tool |

## Tool Calling

Define a function with JSON Schema properties, register a handler, and the library manages the entire conversation loop — including sending tool results back to the model.

```csharp
var api = new CrideApi(new ProviderInfo
{
    EndPoint = new Uri("http://127.0.0.1:8080"),
    Streaming = true
});

// 1. Define the function schema
var getWeather = new ApiFunction
{
    Name = "get_weather",
    Description = "Get the current weather for a city.",
    Properties = new Dictionary<string, FunctionProperties>
    {
        ["city"] = new FunctionProperties
        {
            Type = "string",
            Description = "City name, e.g. 'Budapest'"
        },
        ["unit"] = new FunctionProperties
        {
            Type = "string",
            Description = "Temperature unit: 'celsius' or 'fahrenheit'"
        }
    },
    Required = new[] { "city" }
};

// 2. Register the handler
api.AddFunction(getWeather, async args =>
{
    var doc = JsonDocument.Parse(args);
    var city = doc.RootElement.GetProperty("city").GetString();
    var unit = doc.RootElement.TryGetProperty("unit", out var u)
        ? u.GetString() : "celsius";
    
    // Call your weather API here...
    return $$"""{"city":"{{city}}","temperature":22,"unit":"{{unit}}"}""";
});

// 3. (Optional) Subscribe to tool execution events
api.ToolCallExecuted += (tool, result) =>
    Console.WriteLine($"[tool] {tool.Function?.Name} => {result}");

// 4. Ask a question that triggers the tool
await foreach (var delta in api.GetResponseAsync("What's the weather in Budapest?"))
{
    if (delta.Role == "assistant")
        Console.Write(delta.Content);
}
```

### How the tool loop works

1. The user message is sent along with the function definitions.
2. If the model responds with `finish_reason: "tool_calls"`, the library extracts the tool calls.
3. Each tool's registered handler is executed.
4. Results are posted back as `role: "tool"` messages.
5. The model generates a final response incorporating the tool results.
6. Steps 2–5 repeat if the model issues more tool calls.

## Project Structure

```
CrideLLMApi/
├── Api/
│   └── CrideApi.cs          # Core client: streaming, tool loop, SSE parsing
├── DTO/
│   ├── ApiFunction.cs       # Function/tool definition DTOs
│   ├── ChatMessage.cs       # Chat message record
│   ├── ProviderInfo.cs      # Connection/provider configuration
│   └── ResponseJson.cs      # SSE response deserialization records
├── Helpers/
│   └── ApiFlags.cs          # Enums: API_MODE, REQUEST_ROLE, REASONING_EFFORT, TOOL_CHOICE
├── Program.cs               # Example/demo entry point
├── CrideLLMApi.csproj
└── CrideLLMApi.slnx
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An OpenAI-compatible API server (e.g., [LM Studio](https://lmstudio.ai/), [Ollama](https://ollama.com/), [llama.cpp server](https://github.com/ggerganov/llama.cpp), or the OpenAI API itself)

## License

This project is provided as-is. See the repository for license details.
