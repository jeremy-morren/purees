using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PureES.Core.EventStore;

namespace PureES.EventStoreDB.Serialization;

public static class EventStoreDBSerializerServiceCollectionExtensions
{
    public static IServiceCollection AddEventStoreDBSerializer<TMetadata>(this IServiceCollection services)
        where TMetadata : notnull
    {
        services.TryAddTransient<IEventStoreDBSerializer, EventStoreDBSerializer<TMetadata>>();
        return services;
    }
}