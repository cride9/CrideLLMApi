# CrideLLMApi

A lightweight, streaming-first .NET 10 client library for working with OpenAI-compatible LLM APIs.

CrideLLMApi is designed around **low-level access and explicit control**. It exposes raw streamed response chunks, tool-call deltas, reasoning content, finish reasons, request objects, conversation context, and tool execution while still providing convenient higher-level APIs when you do not need that control.

The library is primarily developed around local OpenAI-compatible servers such as **llama.cpp**, while remaining compatible with remote OpenAI-style APIs.

## Why CrideLLMApi?

Most high-level LLM libraries hide much of the underlying API behavior behind abstractions.

CrideLLMApi takes the opposite approach.

You can:

* consume every SSE chunk sent by the server
* inspect streamed tool-call fragments
* access reasoning deltas
* cancel an active generation
* execute exactly one completion request without an automatic tool loop
* build and modify requests manually
* manage conversation context explicitly
* register strongly typed tools without implementing interfaces
* execute multiple tool calls concurrently
* subscribe to tool execution events

At the same time, higher-level helpers are available for ordinary conversations and automatic tool execution.

> **No black boxes.** The high-level API is optional; the underlying streaming and request layers remain directly accessible.

---

# Features

* **Streaming-first API** using `IAsyncEnumerable<T>`
* **Raw SSE access** through `ChatCompletionChunk`
* **Reasoning stream access** through `reasoning_content`
* **OpenAI-compatible tool/function calling**
* **Strongly typed tool arguments**
* **Concurrent tool execution**
* **Automatic OpenAI-style tool loop**
* **Single-request completion streaming**
* **End-to-end `CancellationToken` support**
* **Explicit conversation context management**
* **Custom system, user, assistant, and tool messages**
* **Typed `ChatCompletionRequest` objects**
* **Manual request creation and modification**
* **Event-driven tool execution**
* **Local and authenticated API support**
* **Reasoning effort configuration**
* **Tool choice configuration**
* **Minimal dependencies**

---

# Requirements

* .NET 10 SDK
* An OpenAI-compatible API server

Examples include:

* llama.cpp server
* LM Studio
* Ollama
* OpenAI API
* other OpenAI-compatible inference servers

---

# Installation

Clone the repository and add a project reference:

```xml
<ProjectReference Include="..\CrideLLMApi\CrideLLMApi.csproj" />
```

NuGet packaging may be added later.

---

# Quick Start

## Create the API client

```csharp
using CrideLLMApi.Api;
using CrideLLMApi.Api.Endpoints;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

var api = new CrideApi(new ProviderInfo
{
    EndPoint = new Uri("http://127.0.0.1:8080"),
    Streaming = true,
    ReasoningEffort = REASONING_EFFORT.NONE,
    ToolChoice = TOOL_CHOICE.AUTO
});
```

Retrieve the endpoint implementation:

```csharp
var chatCompletion =
    api.GetEndpointMethods<ChatCompletion>();

if (chatCompletion is null)
    return;
```

CrideApi acts as the entry point for the available API endpoint implementations.

---

# Conversation Context

Conversation state is managed through `ContextManager`.

```csharp
var context = new ContextManager();

chatCompletion.AddContextManager(context);
```

Messages can be added manually:

```csharp
context.Add(new ChatMessage
{
    Role = "system",
    Content = "You are a concise programming assistant."
});
```

Convenience methods can also be used for common message types:

```csharp
context.AddSystem(
    "You are a concise programming assistant.");

context.AddUser(
    "Explain dependency injection.");

context.AddAssistant(
    "Dependency injection provides dependencies externally.");

context.AddTool(
    "call_123",
    "Tool execution result");
```

You can also add messages by role:

```csharp
context.Add(
    REQUEST_ROLE.SYSTEM,
    "Always answer in English.");
```

## Replace the system instruction

```csharp
context.SetSystem(
    "You are an expert C# and .NET assistant.");
```

This replaces the existing system instruction instead of appending another one.

## Add multiple messages

```csharp
context.AddRange(messages);
```

## Read conversation history

```csharp
var messages = context.GetMessages();
```

Filter by role:

```csharp
var toolMessages =
    context.GetMessages(REQUEST_ROLE.TOOL);
```

## Reset conversation memory

```csharp
context.NewMemory();
```

Or replace it with an existing conversation:

```csharp
context.NewMemory(existingMessages);
```

A copy of the current memory can be retrieved with:

```csharp
var memory = context.GetMemory();
```

---

# Basic Streaming

`GetResponseAsync` exposes the convenient content stream.

```csharp
await foreach (
    var delta in chatCompletion.GetResponseAsync(
        "Explain quantum computing in one sentence."))
{
    Console.Write(delta.Content);
}
```

It returns:

```csharp
IAsyncEnumerable<Delta>
```

and filters the lower-level response stream down to assistant deltas.

---

# Raw Streaming

For full access to the API stream, use:

```csharp
await foreach (
    var chunk in chatCompletion.GetRawResponseAsync(
        "Explain quantum computing."))
{
    Console.WriteLine(chunk);
}
```

This exposes:

```csharp
IAsyncEnumerable<ChatCompletionChunk>
```

including fields such as:

* response ID
* model
* response metadata
* assistant content
* reasoning content
* tool-call deltas
* tool arguments
* finish reason

For example:

```csharp
await foreach (
    var chunk in chatCompletion.GetRawResponseAsync(
        "Solve this problem step by step."))
{
    var delta = chunk.Choices[0].Delta;

    if (delta.ReasoningContent is not null)
        Console.Write(delta.ReasoningContent);

    if (delta.Content is not null)
        Console.Write(delta.Content);
}
```

---

# Cancellation

Streaming operations support `CancellationToken`.

```csharp
using var cts = new CancellationTokenSource();

await foreach (
    var chunk in chatCompletion.GetRawResponseAsync(
        "Write a very long response.",
        cts.Token))
{
    Console.Write(chunk.Choices[0].Delta.Content);

    if (ShouldStop())
        cts.Cancel();
}
```

Cancellation is propagated through the complete streaming pipeline, including the HTTP request and SSE reader.

This makes an active generation interruptible by external orchestration code.

---

# Single Completion Requests

`GetRawResponseAsync` provides the normal conversation behavior, including the automatic tool loop.

When you need **exactly one inference request**, use `StreamCompletionAsync`.

```csharp
var request = chatCompletion.CreateRequest();

await foreach (
    var chunk in chatCompletion.StreamCompletionAsync(request))
{
    Console.Write(chunk.Choices[0].Delta.Content);
}
```

`StreamCompletionAsync` performs one request:

```text
request
   ↓
POST /v1/chat/completions
   ↓
SSE stream
   ↓
complete
```

It does not automatically continue the OpenAI tool loop.

This provides a low-level primitive for callers that want to control generation boundaries themselves.

---

# Creating Requests

CrideLLMApi exposes a typed `ChatCompletionRequest`.

Create one using the current endpoint configuration:

```csharp
var request =
    chatCompletion.CreateRequest();
```

Or provide an explicit message collection:

```csharp
var request =
    chatCompletion.CreateRequest(customMessages);
```

The generated request contains the configured:

* model
* messages
* registered tools
* streaming setting
* reasoning effort
* tool choice

Because it is a normal DTO, it can be inspected or modified before being sent.

Example:

```csharp
var request = chatCompletion.CreateRequest(messages);

request = request with
{
    Model = "my-model"
};
```

Then:

```csharp
await foreach (
    var chunk in chatCompletion.StreamCompletionAsync(
        request,
        cancellationToken))
{
    // handle raw stream
}
```

---

# Tool Calling

Tools are defined using `ApiFunction`.

```csharp
var getWeather = new ApiFunction
{
    Name = "get_current_weather",

    Description =
        "Get the current weather in a given location",

    Properties = new()
    {
        ["name"] = new FunctionProperties
        {
            Type = "string",
            Description = "Location name"
        },

        ["unit"] = new FunctionProperties
        {
            Type = "string",
            Description = "celsius or fahrenheit"
        }
    },

    Required = ["name", "unit"]
};
```

---

# Strongly Typed Tool Handlers

Tool handlers can use strongly typed argument objects.

```csharp
public sealed class GetWeatherArgs
{
    public string name { get; set; } = string.Empty;
    public string unit { get; set; } = string.Empty;
}
```

Register the tool:

```csharp
chatCompletion.AddFunction<GetWeatherArgs>(
    getWeather,
    async args =>
    {
        Console.WriteLine(
            $"[TOOL CALL] Weather requested for {args.name}");

        return
            $"The current weather in {args.name} is " +
            $"25 degrees {args.unit}.";
    });
```

The JSON tool arguments returned by the model are automatically deserialized into `GetWeatherArgs`.

No tool interface implementation is required.

A regular method can also be used:

```csharp
chatCompletion.AddFunction<GetWeatherArgs>(
    getWeather,
    GetCurrentWeather);
```

```csharp
public static async Task<string> GetCurrentWeather(
    GetWeatherArgs args)
{
    return
        $"The current weather in {args.name} is " +
        $"25 degrees {args.unit}.";
}
```

---

# Raw Tool Handlers

A raw JSON handler can also be registered.

```csharp
chatCompletion.AddFunction(
    getWeather,
    async argumentsJson =>
    {
        using var document =
            JsonDocument.Parse(argumentsJson);

        var name =
            document.RootElement
                .GetProperty("name")
                .GetString();

        return $"Weather requested for {name}";
    });
```

This is useful when complete control over argument deserialization is required.

---

# Concurrent Tool Execution

Multiple tool calls returned by the same model response are scheduled independently.

Instead of:

```text
tool A
  ↓
wait
  ↓
tool B
  ↓
wait
  ↓
tool C
```

they can execute concurrently:

```text
tool A ────────────────→

tool B ────────→

tool C ─────────────────────→

                ↓
             await all
```

Tool execution state is represented separately from tool registration, allowing scheduling and execution behavior to remain independent from the tool definitions themselves.

---

# Tool Execution Events

Subscribe to `ToolCallExecuted` to observe completed tool calls.

```csharp
chatCompletion.ToolCallExecuted +=
    (tool, result) =>
    {
        Console.WriteLine(
            $"[tool] {tool.Function?.Name} => {result}");
    };
```

Example output:

```text
[TOOL CALL] The current weather in Budapest is 25 degrees celsius.
The weather in Budapest is currently 25 degrees Celsius.
```

`ToolCallExecuted` is exposed by `ChatCompletion`, while the internal scheduler manages the actual tool execution lifecycle.

---

# Automatic Tool Loop

`GetResponseAsync` and `GetRawResponseAsync` support the normal OpenAI-style tool conversation loop.

The process is:

1. Send the user message with the registered tool definitions.
2. Stream the model response.
3. Accumulate streamed tool-call fragments.
4. Detect `finish_reason: "tool_calls"`.
5. Schedule the requested tools.
6. Execute the tools concurrently.
7. Add the assistant tool-call message to the conversation context.
8. Add each tool result as a `role: "tool"` message.
9. Send the updated conversation back to the model.
10. Continue until the model no longer requests tools.

Conceptually:

```text
User
 ↓
Model
 ↓
tool calls
 ↓
ToolScheduler
 ├── Tool A
 ├── Tool B
 └── Tool C
 ↓
results
 ↓
ContextManager
 ↓
Model
 ↓
final response
```

---

# Tool Architecture

Tool definitions and tool execution are intentionally separated.

```text
ApiFunction
    ↓
ToolRegistry
    ├── Function definition
    └── Tool executor
            ↓
      ToolScheduler
            ↓
      ToolExecution
```

## ToolRegistry

Responsible for:

* registering tool schemas
* storing tool executors
* mapping tool names to handlers
* strongly typed argument conversion

## ToolScheduler

Responsible for:

* scheduling executions
* running tools asynchronously
* concurrent execution
* execution lifecycle events

## ToolExecution

Tracks execution state such as:

```text
QUEUED
RUNNING
COMPLETED
FAILED
CANCELLED
```

along with:

* tool call metadata
* result
* exception
* creation time
* start time
* completion time

---

# Registered Functions

Retrieve the currently registered tool definitions:

```csharp
IEnumerable<FunctionCallObject> functions =
    chatCompletion.GetFunctionCalls();
```

---

# Provider Configuration

`ProviderInfo` configures the underlying LLM endpoint.

```csharp
var provider = new ProviderInfo
{
    EndPoint = new Uri("http://127.0.0.1:8080"),
    ModelName = "model-name",
    Streaming = true,
    ReasoningEffort = REASONING_EFFORT.NONE,
    ToolChoice = TOOL_CHOICE.AUTO
};
```

## Remote API

```csharp
var provider = new ProviderInfo
{
    EndPoint = new Uri("https://api.openai.com"),
    ApiKey = "sk-...",
    ModelName = "model-name",
    Streaming = true
};
```

When an API key is provided, CrideLLMApi sends:

```text
Authorization: Bearer <api-key>
```

and switches to OpenAI-compatible authenticated mode.

---

# ProviderInfo

| Field             | Type               | Description                                   |
| ----------------- | ------------------ | --------------------------------------------- |
| `EndPoint`        | `Uri`              | Base URL of the API server                    |
| `ApiKey`          | `string?`          | Optional bearer token                         |
| `ModelName`       | `string?`          | Model sent with requests                      |
| `Streaming`       | `bool`             | Enables response streaming                    |
| `ApiMode`         | `API_MODE`         | Local or authenticated OpenAI-compatible mode |
| `ReasoningEffort` | `REASONING_EFFORT` | Requested reasoning effort                    |
| `ToolChoice`      | `TOOL_CHOICE`      | Tool selection behavior                       |

---

# Endpoint Access

`CrideApi` owns the available API endpoint implementations.

Endpoints can be retrieved by type:

```csharp
var chatCompletion =
    api.GetEndpointMethods<ChatCompletion>();
```

The endpoint registry is intentionally lightweight and does not require an external dependency injection framework.

Conceptually:

```text
CrideApi
   ↓
endpoint registry
   ↓
GetEndpointMethods<T>()
   ↓
ChatCompletion
```

---

# Enums

## API_MODE

```text
LOCAL
OPENAI_COMPATIBLE
```

Controls authentication behavior.

## REQUEST_ROLE

```text
SYSTEM
USER
ASSISTANT
TOOL
```

Represents OpenAI-compatible conversation roles.

## REASONING_EFFORT

```text
NONE
LOW
HIGH
MAX
```

Controls reasoning effort for providers that support it.

## TOOL_CHOICE

```text
AUTO
REQUIRED
```

Controls whether tool usage is optional or required.

---

# Architecture

CrideLLMApi separates API responsibilities instead of placing all behavior inside a single endpoint class.

```text
CrideApi
│
└── ChatCompletion
    │
    ├── ContextManager
    │
    ├── ChatCompletionRequestBuilder
    │
    ├── ChatCompletionTransport
    │
    ├── ToolRegistry
    │
    └── ToolScheduler
    │
    └── streaming / SSE processing
```

### ChatCompletion

Public endpoint facade responsible for:

* response streaming
* raw completion streaming
* tool loop coordination
* context integration
* public tool events

### ChatCompletionRequestBuilder

Responsible for constructing typed chat completion requests using:

* provider configuration
* conversation context
* registered tools

### ChatCompletionTransport

Responsible for HTTP communication with:

```text
POST /v1/chat/completions
```

including streaming response handling setup.

### ContextManager

Responsible for explicit conversation state.

### ToolRegistry

Responsible for tool definitions and handlers.

### ToolScheduler

Responsible for asynchronous execution.

---

# Project Structure

```text
CrideLLMApi/
│
├── Api/
│   ├── CrideApi.cs
│   ├── ContextManager.cs
│   │
│   ├── Endpoints/
│   │   ├── ChatCompletion.cs
│   │   ├── ChatCompletionRequestBuilder.cs
│   │   └── ChatCompletionTransport.cs
│   │
│   └── Tools/
│       ├── RegisteredTool.cs
│       ├── ToolExecution.cs
│       ├── ToolRegistry.cs
│       └── ToolScheduler.cs
│
├── DTO/
│   ├── ApiFunction.cs
│   ├── ChatCompletionRequest.cs
│   ├── ChatMessage.cs
│   ├── ProviderInfo.cs
│   └── ResponseJson.cs
│
├── Helpers/
│   └── ApiFlags.cs
│
├── example.cs
├── CrideLLMApi.csproj
└── CrideLLMApi.slnx
```

---

# Design Philosophy

CrideLLMApi intentionally avoids turning the underlying API into a large abstraction framework.

The goal is to provide:

```text
high-level convenience
        +
low-level control
```

without losing access to the actual protocol.

For ordinary applications:

```csharp
await foreach (
    var delta in chatCompletion.GetResponseAsync("Hello"))
{
    Console.Write(delta.Content);
}
```

For low-level consumers:

```csharp
var request =
    chatCompletion.CreateRequest(customContext);

await foreach (
    var chunk in chatCompletion.StreamCompletionAsync(
        request,
        cancellationToken))
{
    // Full control over every streamed chunk.
}
```

The caller decides how much abstraction it wants.

---

# License

This project is provided as-is. See the repository for license details.
