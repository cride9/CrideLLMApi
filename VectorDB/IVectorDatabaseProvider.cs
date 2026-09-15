using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.Helpers;

namespace CrideLLMApi.VectorDB;

public interface IVectorDatabaseProvider
{
    VectorDatabaseType Type { get; }

    /// <summary>
    /// Creates an instance of IVectorDatabase based on the provided CrideApi and ProviderInfo.
    /// </summary>
    /// <param name="api">The CrideApi instance.</param>
    /// <param name="providerInfo">The provider information.</param>
    /// <returns>The created IVectorDatabase instance.</returns>
    IVectorDatabase Create(
        CrideApi api,
        ProviderInfo providerInfo);
}