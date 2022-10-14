using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PureES.Core.EventStore.Serialization;

public static class EventStoreSerializationServiceCollectionExtensions
{
    public static IServiceCollection AddEventStoreSerializerCore(this IServiceCollection services,
        Action<JsonSerializerOptions>? configureJsonOptions = null,
        Action<BasicEventTypeMap>? configureTypeMap = null)
    {
        services.TryAddSingleton<IEventStoreSerializer>(_ =>
        {
            var options = new JsonSerializerOptions();
            configureJsonOptions?.Invoke(options);
            return new JsonEventStoreSerializer(options);
        });
        
        services.TryAddSingleton<IEventTypeMap>(_ =>
        {
            var map = new BasicEventTypeMap();
            configureTypeMap?.Invoke(map);
            return map;
        });

        return services;
    }
}