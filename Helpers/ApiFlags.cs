namespace CrideLLMApi.Helpers;

[Flags]
public enum API_MODE
{
    LOCAL = 0,
    OPENAI_COMPATIBLE = 1
}

[Flags]
public enum REQUEST_ROLE
{
    SYSTEM,
    USER,
    ASSISTANT,
    TOOL
}

[Flags]
public enum REASONING_EFFORT
{
    NONE,
    LOW,
    HIGH,
    MAX
}

[Flags]
public enum TOOL_CHOICE
{
    AUTO,
    REQUIRED
}