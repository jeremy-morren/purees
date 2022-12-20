using Microsoft.Extensions.Options;
using PureES.Core.ExpBuilders;

namespace PureES.Core;

internal class PureESServices : IServiceProvider
{
    //None of the services are disposable, so we don't need to dispose the service provider

    private readonly IServiceProvider _services;

    private PureESServices(IServiceProvider services) => _services = services;

    public PureESServices(IOptions<CommandHandlerOptions> options)
        : this(BuildServiceProvider(options.Value))
    {
    }

    public object? GetService(Type serviceType) => _services.GetService(serviceType);

    private static IServiceProvider BuildServiceProvider(CommandHandlerOptions options)
    {
        var services = new ServiceCollection();
        var aggregateTypes = options.Assemblies
            .SelectMany(t => t.GetExportedTypes())
            .Where(t => t.GetCustomAttribute(typeof(AggregateAttribute)) != null);
        foreach (var t in aggregateTypes)
            AddAggregateServices(services, t, options.BuilderOptions);
        return services.BuildServiceProvider();
    }

    public static PureESServices Build(Type aggregateType, CommandHandlerBuilderOptions options)
    {
        var services = new ServiceCollection();
        AddAggregateServices(services, aggregateType, options);
        return new PureESServices(services.BuildServiceProvider());
    }

    private static void AddAggregateServices(IServiceCollection services,
        Type aggregateType,
        CommandHandlerBuilderOptions options)
    {
        var builder = new CommandServicesBuilder(options);
        builder.AddCommandHandlers(aggregateType, services);

        //Add load method
        var load = builder.CompileFactory(aggregateType, out var delegateType);
        services.Add(new ServiceDescriptor(delegateType, load));
    }
}