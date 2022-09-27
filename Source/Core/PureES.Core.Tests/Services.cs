using System;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PureES.Core.Tests;

public static class Services
{
    [Pure]
    public static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
    
    /// <summary>
    /// Builds a service collection
    /// using provided <paramref name="configure"/>
    /// and attempts to active
    /// an instance of <see cref="CommandHandler{T}"/>
    /// </summary>
    /// <param name="configure">Configure services</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [Pure]
    public static CommandHandler<T> GetCommandHandler<T>(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        using var provider = services.BuildServiceProvider();
        var handler = provider.GetService<CommandHandler<T>>();
        return handler ??
               throw new InvalidOperationException($"Unable to create instance of {typeof(CommandHandler<T>)}");
    }
}