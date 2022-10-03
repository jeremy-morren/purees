using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PureES.Core.ExpBuilders.Services;

internal class PureESServices : IServiceProvider
{
    //None of the services are disposable, so we don't need to dispose the service provider
    
    private readonly IServiceProvider _services;

    public PureESServices(IOptions<CommandHandlerOptions> options)
        => _services = BuildServiceProvider(options.Value);

    public object? GetService(Type serviceType) => _services.GetService(serviceType);
    
    private static IServiceProvider BuildServiceProvider(CommandHandlerOptions options)
    {
        var services = new ServiceCollection();
        var builder = new CommandServicesBuilder(options.BuilderOptions);
        var aggregateTypes = options.Assemblies
            .SelectMany(t => t.GetExportedTypes())
            .Where(t => t.GetCustomAttribute(typeof(AggregateAttribute)) != null);
        foreach (var t in aggregateTypes)
        {
            builder.AddCommandHandlers(t, services);
            
            //Add load method
            var load = builder.Factory(t);
            services.Add(new ServiceDescriptor(load.Type, load.Value));
        }
        return services.BuildServiceProvider();
    }
}