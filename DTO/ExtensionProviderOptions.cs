using CrideLLMApi.Helpers;
using CrideLLMApi.VectorDB;

namespace CrideLLMApi.DTO;

/// <summary>
/// Configures the optional extensions, currently the vector database used through
/// <see cref="IVectorDatabase"/> and <see cref="VectorDatabaseRegistry"/>.
/// </summary>
public sealed record ExtensionProviderOptions
{
    /// <summary>
    /// Gets the vector database implementation to use. <see cref="VectorDatabaseType.NONE"/>
    /// disables the vector database and makes
    /// <see cref="Api.CrideApi.GetExtension{T}"/> return null.
    /// </summary>
    public VectorDatabaseType VectorDB { get; init; } = VectorDatabaseType.NONE;

    /// <summary>
    /// Gets the endpoint of an already running vector database server. When null, the extension
    /// falls back to its own default address.
    /// </summary>
    public Uri? VectorDBEndPoint { get; init; }

    /// <summary>
    /// Gets the API key used to authenticate against the vector database server, when it requires one.
    /// </summary>
    public string? VectorDBApiKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the extension may start the vector database server itself.
    /// </summary>
    public bool AutoStartVectorDB { get; init; } = true;

    /// <summary>
    /// Gets the directory where the extension stores its runtime files, for example the managed
    /// Python environment used by the ChromaDB extension.
    /// </summary>
    public string? VectorDBRuntimePath { get; init; }

    /// <summary>
    /// Gets the directory where the vector database stores its data.
    /// </summary>
    public string? VectorDBDataPath { get; init; }

    /// <summary>Preinstalled vector database CLI used by an offline runtime.</summary>
    public string? VectorDBExecutablePath { get; init; }

    /// <summary>Whether the extension may download packages during startup.</summary>
    public bool InstallVectorDBOnStartup { get; init; } = true;

    /// <summary>Whether vector database process output is forwarded to the console.</summary>
    public bool ForwardVectorDBLogs { get; init; } = true;
}
