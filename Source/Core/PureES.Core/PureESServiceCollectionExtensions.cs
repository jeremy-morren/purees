using Microsoft.Extensions.DependencyInjection.Extensions;
using PureES.Core.EventStore;
using PureES.Core.ExpBuilders.EventHandlers;

// ReSharper disable UnusedMember.Global

namespace PureES.Core;

public static class PureESServiceCollectionExtensions
{
    /// <summary>
    ///     Adds PureES services from for <paramref name="assemblies" />
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IServiceCollection AddPureES(this IServiceCollection services,
        Assembly[] assemblies,
        PureESBuilderOptions options)
    {
        services.AddPureESCore();
        services.Configure<PureESOptions>(o =>
        {
            foreach (var a in assemblies)
                o.AddAssembly(a);
            o.BuilderOptions = options;
        });
        
        AddEventHandlerServices(services, assemblies, options);
        return services;
    }

    /// <summary>
    ///     Adds PureES services from assembly
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddPureES(this IServiceCollection services, PureESBuilderOptions options)
    {
        var entryAssembly = Assembly.GetEntryAssembly() ??
                            throw new InvalidOperationException("Unable to locate Entry Assembly");
        services.AddPureES(new[] {entryAssembly}, options);
        
        return services;
    }
    
    /// <summary>
    ///     Adds core PureES services
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddPureESCore(this IServiceCollection services)
    {
        services.TryAddSingleton<PureESServices>();
        services.TryAddTransient(typeof(ICommandHandler<>), typeof(CommandHandler<>));
        services.TryAddTransient(typeof(IAggregateStore<>), typeof(AggregateStore<>));
        services.TryAddTransient(typeof(IEventHandlersCollection), typeof(EventHandlersCollection));
        
        return services;
    }

    private static void AddEventHandlerServices(this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        PureESBuilderOptions builderOptions)
    {
        var options = new PureESOptions()
        {
            BuilderOptions = builderOptions
        };
        foreach (var a in assemblies)
            options.AddAssembly(a);
        var builder = new EventHandlerServicesBuilder(options);
        builder.AddEventHandlerServices(services);
    }

    public static IServiceCollection AddBasicEventTypeMap(this IServiceCollection services, 
        Action<BasicEventTypeMap> configure)
    {
        services.AddSingleton<IEventTypeMap>(_ =>
        {
            var map = new BasicEventTypeMap();
            configure(map);
            return map;
        });

        return services;
    }
}