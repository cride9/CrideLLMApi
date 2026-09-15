using CrideLLMApi.Helpers;
using CrideLLMApi.VectorDB;

namespace CrideLLMApi.DTO;

public sealed record ExtensionProviderOptions
{
    public VectorDatabaseType VectorDB { get; init; } = VectorDatabaseType.NONE;

    public Uri? VectorDBEndPoint { get; init; }

    public string? VectorDBApiKey { get; init; }

    public bool AutoStartVectorDB { get; init; } = true;

    public string? VectorDBRuntimePath { get; init; }

    public string? VectorDBDataPath { get; init; }
}