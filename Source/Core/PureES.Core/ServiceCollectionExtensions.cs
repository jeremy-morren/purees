using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.Core;

public static class ServiceCollectionExtensions
{
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
        return services;
    }

    /// <summary>
    ///     Adds PureES services from for <paramref name="assemblies" />
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IServiceCollection AddPureES(this IServiceCollection services,
        Assembly[] assemblies,
        CommandHandlerBuilderOptions options)
    {
        services.AddPureESCore();
        services.Configure<CommandHandlerOptions>(o =>
        {
            foreach (var a in assemblies)
                o.AddAssembly(a);
            o.BuilderOptions = options;
        });
        return services;
    }

    /// <summary>
    ///     Adds PureES services from assembly
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddPureES(this IServiceCollection services,
        CommandHandlerBuilderOptions options)
    {
        var entryAssembly = Assembly.GetEntryAssembly() ??
                            throw new InvalidOperationException("Unable to locate Entry Assembly");
        services.AddPureESCore();
        services.Configure<CommandHandlerOptions>(o =>
        {
            o.AddAssembly(entryAssembly);
            o.BuilderOptions = options;
        });
        return services;
    }
}