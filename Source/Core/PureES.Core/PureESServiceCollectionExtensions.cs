using Microsoft.Extensions.DependencyInjection;
using PureES.Core.EventStore;
using PureES.Core.Generators;

namespace PureES.Core;

[PublicAPI]
public static class PureESServiceCollectionExtensions
{
    /// <summary>
    /// Adds core PureES services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddPureESCore(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        services.AddOptions<PureESOptions>()
            .Validate(o =>
            {
                o.Validate();
                return true;
            });
        
        if (services.All(d => d.ServiceType != typeof(IEventTypeMap)))
            services.AddSingleton<IEventTypeMap, BasicEventTypeMap>();
        if (services.All(d => d.ServiceType != typeof(PureESStreamId<>)))
            services.AddSingleton(typeof(PureESStreamId<>));

        return services;
    }
    
    /// <summary>
    /// Register PureES services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Configure PureES options</param>
    /// <param name="assemblies">Assemblies to scan for PureES services</param>
    /// <returns>The service collection so that further calls can be chained</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddPureES(this IServiceCollection services,
        Action<PureESOptions> configureOptions,
        params Assembly[] assemblies)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

        services.AddPureESCore();

        services.AddOptions<PureESOptions>()
            .Configure(o =>
            {
                configureOptions(o);
                foreach (var a in assemblies)
                    o.Assemblies.Add(a);
            });
        
        //Scan assemblies for service registry type
        foreach (var a in assemblies.Distinct())
        {
            var type = a.GetTypes()
                .FirstOrDefault(t => t.FullName == DependencyInjectionGenerator.FullClassName);
            if (type == null) continue; //Revisit: should probably throw
            var method = type.GetMethod(DependencyInjectionGenerator.MethodName,
                             BindingFlags.NonPublic | BindingFlags.Static)
                         ?? throw new NotImplementedException($"Unable to get register method for assembly {a}");
            method.Invoke(null, new object?[] { services });
        }
        
        return services;
    }
}