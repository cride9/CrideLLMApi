namespace CrideLLMApi.Helpers;

[Flags]
public enum API_MODE
{
    LOCAL = 0,
    OPENAI_COMPATIBLE = 1,
    COUNT
}

[Flags]
public enum REQUEST_ROLE
{
    SYSTEM,
    USER,
    ASSISTANT,
    TOOL,
    COUNT
}

[Flags]
public enum REASONING_EFFORT
{
    NONE,
    LOW,
    HIGH,
    MAX,
    COUNT
}

[Flags]
public enum TOOL_CHOICE
{
    AUTO,
    REQUIRED,
    COUNT
}

[Flags]
public enum TOOL_EXECUTION_MODE
{
    OPENAI_LOOP,
    ASYNC,
    COUNT
}

[Flags]
public enum ToolExecutionStatus
{
    QUEUED,
    RUNNING,
    COMPLETED,
    FAILED,
    CANCELLED,
    COUNT
}