using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.VectorDB;

public static class VectorDatabaseRegistry
{
    private static readonly Dictionary<
        VectorDatabaseType,
        IVectorDatabaseProvider> Providers = new();

    /// <summary>
    /// Registers a vector database provider.
    /// </summary>
    /// <param name="provider">The vector database provider to register.</param>
    public static void Register(
        IVectorDatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Providers[provider.Type] = provider;
    }

    /// <summary>
    /// Creates an instance of a vector database based on the specified type, API, and provider information.
    /// </summary>
    /// <param name="type">The type of the vector database to create.</param>
    /// <param name="api">The CrideApi instance.</param>
    /// <param name="providerInfo">The provider information.</param>
    /// <returns>The created IVectorDatabase instance, or null if the type is NONE.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IVectorDatabase? Create(
        VectorDatabaseType type,
        CrideApi api,
        ProviderInfo providerInfo)
    {
        if (type == VectorDatabaseType.NONE)
            return null;

        if (!Providers.TryGetValue(
                type,
                out var provider))
        {
            throw new InvalidOperationException(
                $"Vector database provider '{type}' is not registered.");
        }

        return provider.Create(
            api,
            providerInfo);
    }
}