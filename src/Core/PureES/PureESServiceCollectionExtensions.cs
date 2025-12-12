using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PureES;

[PublicAPI]
public static class PureESServiceCollectionExtensions
{
    /// <summary>
    /// Registers core PureES services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Configure PureES options</param>
    /// <returns>The builder so that further calls can be chained</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static PureESBuilder AddPureES(this IServiceCollection services, Action<PureESOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient(typeof(IEventHandlersProvider), typeof(EventHandlersProvider));
        services.TryAddTransient(typeof(EventHandlerCollection<>));

        services.AddOptions<PureESOptions>()
            .Validate(o => o.Validate());
        if (configureOptions != null)
            services.Configure(configureOptions);

        return new PureESBuilder(services);
    }

    /// <summary>
    /// Adds PureES services registered in <paramref name="assembly"/>
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="assembly"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    public static PureESBuilder AddFromAssembly(this PureESBuilder builder, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        //See SourceGenerator DependencyInjectionGenerator.cs

        const string fullClassName = "PureES.DependencyInjection.PureESServiceCollectionExtensions";
        const string methodName = "Register";
        
        var type = assembly.GetTypes().FirstOrDefault(t => t.FullName == fullClassName);
        if (type == null) throw new NotImplementedException("No PureES services found");
        
        var method = type.GetMethod(methodName, 
                         BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new NotImplementedException($"Unable to get register method for assembly {assembly}");
        method.Invoke(null, [builder.Services]);
        
        return builder;
    }

    /// <summary>
    /// Adds a basic <see cref="IEventTypeMap"/> that uses the fully qualified type name
    /// </summary>
    public static PureESBuilder AddBasicEventTypeMap(this PureESBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.RemoveAll(typeof(IEventTypeMap));
        builder.Services.AddSingleton<IEventTypeMap, BasicEventTypeMap>();
        
        return builder;
    }

    /// <summary>
    /// Adds a basic <see cref="IAggregateStore{T}"/> that does not include any snapshotting
    /// </summary>
    public static PureESBuilder AddBasicAggregateStore(this PureESBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.RemoveAll(typeof(IAggregateStore<>));
        builder.Services.AddTransient(typeof(IAggregateStore<>), typeof(BasicAggregateStore<>));

        return builder;
    }

    /// <summary>
    /// Adds an implementation of <see cref="ICommandStreamId{TCommand}"/> that
    /// gets a stream id from command properties recursively
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="getStreamIdProperty">A delegate to get the stream id property for a type</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static PureESBuilder AddCommandPropertyStreamId(this PureESBuilder builder,
        GetCommandStreamIdProperty? getStreamIdProperty = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.RemoveAll(typeof(GetCommandStreamIdProperty));
        builder.Services.RemoveAll(typeof(ICommandStreamId<>));
        
        builder.Services.AddSingleton(
            getStreamIdProperty ?? CommandPropertyStreamId<object>.DefaultGetStreamIdProperty);
        builder.Services.AddSingleton(typeof(ICommandStreamId<>), typeof(CommandPropertyStreamId<>));

        return builder;
    }

    /// <summary>
    /// Configures PureES options
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static PureESBuilder Configure(this PureESBuilder builder, Action<PureESOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        return builder;
    }
}