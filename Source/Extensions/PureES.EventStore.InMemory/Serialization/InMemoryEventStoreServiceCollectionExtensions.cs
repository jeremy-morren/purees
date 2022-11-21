using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PureES.Core.EventStore.Serialization;

namespace PureES.EventStore.InMemory.Serialization;

public static class InMemoryEventStoreServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryEventStoreSerializer<TMetadata>(this IServiceCollection services)
        where TMetadata : notnull
    {
        services.AddEventStoreSerializerCore();
        services.TryAddSingleton<IInMemoryEventStoreSerializer, InMemoryEventStoreSerializer<TMetadata>>();
        return services;
    }
}