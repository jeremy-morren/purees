using Microsoft.Extensions.Options;
using PureES.Core.ExpBuilders;
using PureES.Core.ExpBuilders.EventHandlers;

namespace PureES.Core;

internal class PureESServices : IServiceProvider
{
    //None of the services are disposable, so we don't need to dispose the service provider

    private readonly IServiceProvider _services;

    private PureESServices(IServiceProvider services) => _services = services;

    public PureESServices(IOptions<PureESOptions> options)
        : this(BuildServiceProvider(options.Value))
    {
        
    }

    public object? GetService(Type serviceType) => _services.GetService(serviceType);

    private static IServiceProvider BuildServiceProvider(PureESOptions options)
    {
        var services = new ServiceCollection();
        
        var aggregateTypes = options.Assemblies
            .SelectMany(t => t.GetExportedTypes())
            .Where(t => t.GetCustomAttribute(typeof(AggregateAttribute)) != null);
        foreach (var t in aggregateTypes)
            AddAggregateServices(services, t, options.BuilderOptions);
        
        services.AddSingleton(_ =>
            new EventHandlerServicesBuilder(options).BuildEventHandlers());
        
        return services.BuildServiceProvider();
    }

    public static PureESServices Build(Type aggregateType, PureESBuilderOptions options)
    {
        var services = new ServiceCollection();
        AddAggregateServices(services, aggregateType, options);
        return new PureESServices(services.BuildServiceProvider());
    }

    private static void AddAggregateServices(IServiceCollection services,
        Type aggregateType,
        PureESBuilderOptions options)
    {
        var builder = new CommandServicesBuilder(options);
        builder.AddCommandHandlers(aggregateType, services);

        //Add load method
        var load = builder.CompileFactory(aggregateType, out var delegateType);
        services.Add(new ServiceDescriptor(delegateType, load));
    }
}