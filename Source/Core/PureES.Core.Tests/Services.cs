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
        services.AddTransient(typeof(IAggregateStore<>), typeof(AggregateStore<>));
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Pure]
    public static ICommandHandler<T> GetCommandHandler<T>(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICommandHandler<T>>();
    }
}