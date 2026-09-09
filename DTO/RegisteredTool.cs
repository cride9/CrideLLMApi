using CrideLLMApi.Api.Tools;
namespace CrideLLMApi.DTO;

public sealed record RegisteredTool(
    FunctionCallObject Definition,
    ToolExecutor? Executor
);
