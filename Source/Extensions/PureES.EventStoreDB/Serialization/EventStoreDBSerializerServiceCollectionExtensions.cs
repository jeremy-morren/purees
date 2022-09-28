using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace PureES.EventStoreDB.Serialization;

public static class EventStoreDBSerializerServiceCollectionExtensions
{
    public static IServiceCollection AddEventStoreDBSerializer<TMetadata>(this IServiceCollection services,
        Action<JsonSerializerOptions> configureJsonOptions,
        Action<TypeMapper> configureTypeMapper)
        where TMetadata : notnull
    {
        return services.AddSingleton(_ =>
            {
                var mapper = new TypeMapper();
                configureTypeMapper(mapper);
                return mapper;
            })
            .AddTransient<IEventStoreDBSerializer>(sp =>
            {
                var options = new JsonSerializerOptions();
                configureJsonOptions(options);
                return new EventStoreDBSerializer<TMetadata>(options, sp.GetRequiredService<TypeMapper>());
            });
    }
}