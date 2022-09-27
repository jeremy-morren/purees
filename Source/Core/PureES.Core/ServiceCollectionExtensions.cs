using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services,
        CommandHandlerOptions? options = null)
    {
        var entryAssembly = Assembly.GetEntryAssembly() ??
                            throw new InvalidOperationException("Unable to locate Entry Assembly");
        return services.AddCommandHandlers(entryAssembly, options ?? new CommandHandlerOptions());
    }

    public static IServiceCollection AddCommandHandlers(this IServiceCollection services,
        Assembly assembly,
        CommandHandlerOptions options)
    {
        var aggregateTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute(typeof(AggregateAttribute)) != null);
        var builder = new CommandHandlerBuilder(options);
        foreach (var t in aggregateTypes)
            builder.AddCommandServices(services, t);
        return services;
    }
}