using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;
using System.Collections.Concurrent;

namespace CrideLLMApi.VectorDB;

/// <summary>
/// Registry of the available vector database providers. Extension packages register their
/// provider here, typically from a module initializer, and
/// <see cref="CrideApi"/> resolves the provider selected through
/// <see cref="ExtensionProviderOptions.VectorDB"/> from this registry.
/// </summary>
public static class VectorDatabaseRegistry
{
    private const string ComponentName = "VectorDatabaseRegistry";

    private static readonly ConcurrentDictionary<
        VectorDatabaseType,
        IVectorDatabaseProvider> Providers = new();

    /// <summary>
    /// Registers a vector database provider, replacing a previously registered provider for the
    /// same <see cref="VectorDatabaseType"/>.
    /// </summary>
    /// <param name="provider">The vector database provider to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the provider declares a reserved type
    /// (<see cref="VectorDatabaseType.NONE"/> or <see cref="VectorDatabaseType.COUNT"/>), which
    /// would make the provider unreachable.
    /// </exception>
    public static void Register(
        IVectorDatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Type is VectorDatabaseType.NONE or VectorDatabaseType.COUNT)
        {
            throw new CrideApiException(
                message:
                    $"The provider '{provider.GetType().FullName}' declares the reserved vector database type '{provider.Type}', so it can never be selected.",
                component: ComponentName,
                operation: "Register a vector database provider",
                hint:
                    $"Return a concrete type, for example {nameof(VectorDatabaseType.CHROMADB)}, from {nameof(IVectorDatabaseProvider)}.Type.");
        }

        Providers[provider.Type] = provider;
    }

    /// <summary>
    /// Gets the vector database types that currently have a registered provider.
    /// </summary>
    /// <returns>The registered vector database types.</returns>
    public static IReadOnlyCollection<VectorDatabaseType> RegisteredTypes =>
        Providers.Keys
            .OrderBy(type => type.ToString())
            .ToArray();

    /// <summary>
    /// Creates an instance of a vector database based on the specified type, API, and provider information.
    /// </summary>
    /// <param name="type">The type of the vector database to create.</param>
    /// <param name="api">The CrideApi instance.</param>
    /// <param name="providerInfo">The provider information.</param>
    /// <returns>The created IVectorDatabase instance, or null if the type is NONE.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api"/> or <paramref name="providerInfo"/> is null.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when no provider is registered for <paramref name="type"/>, or when the registered
    /// provider fails to create the database.
    /// </exception>
    public static IVectorDatabase? Create(
        VectorDatabaseType type,
        CrideApi api,
        ProviderInfo providerInfo)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(providerInfo);

        if (type == VectorDatabaseType.NONE)
            return null;

        if (!Providers.TryGetValue(
                type,
                out var provider))
        {
            throw new CrideApiException(
                message:
                    $"No vector database provider is registered for '{type}', so the configured extension cannot be created.",
                component: ComponentName,
                operation: $"Create the '{type}' vector database extension",
                hint:
                    "Install and reference the extension package that provides this type, or call " +
                    $"'{nameof(VectorDatabaseRegistry)}.{nameof(Register)}' with your own " +
                    $"{nameof(IVectorDatabaseProvider)} before creating the {nameof(CrideApi)}. " +
                    $"Registered types: {DescribeRegisteredTypes()}.");
        }

        try
        {
            var database = provider.Create(
                api,
                providerInfo);

            if (database is null)
            {
                throw new CrideApiException(
                    message:
                        $"The vector database provider '{provider.GetType().FullName}' returned no database instance for '{type}'.",
                    component: ComponentName,
                    operation: $"Create the '{type}' vector database extension",
                    hint:
                        "Check the provider implementation: IVectorDatabaseProvider.Create must return a usable IVectorDatabase instance.");
            }

            return database;
        }
        catch (CrideApiException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CrideApiException(
                message:
                    $"The vector database provider '{provider.GetType().FullName}' failed to create a '{type}' database: {exception.Message}",
                component: ComponentName,
                operation: $"Create the '{type}' vector database extension",
                innerException: exception,
                hint:
                    "Check the extension configuration in ProviderInfo.Extensions (endpoint, API key and runtime/data paths) and whether the vector database server can be started.");
        }
    }

    private static string DescribeRegisteredTypes()
    {
        var types = RegisteredTypes;

        return types.Count == 0
            ? "<none>"
            : string.Join(", ", types);
    }
}
