using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PureES;

/// <summary>
/// Builder for PureES services.
/// </summary>
public class PureESBuilder
{
    public IServiceCollection Services { get; }

    public PureESBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// Adds a custom event type map to the PureES configuration.
    /// This will replace any existing event type map.
    /// </summary>
    public PureESBuilder AddEventTypeMap<T>() where T : class, IEventTypeMap
    {
        Services.RemoveAll<IEventTypeMap>();
        Services.AddSingleton<IEventTypeMap, T>();
        return this;
    }
}