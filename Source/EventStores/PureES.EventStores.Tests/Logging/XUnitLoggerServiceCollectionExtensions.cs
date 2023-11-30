using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace PureES.EventStores.Tests.Logging;

public static class XUnitLoggerServiceCollectionExtensions
{
    public static IServiceCollection AddTestLogging(this IServiceCollection services, 
        ITestOutputHelper? output,
        LogEventLevel minimumLevel = LogEventLevel.Warning)
    {
        services.AddLogging(lb => lb.ClearProviders());

        services.RemoveAll(typeof(ILoggerFactory));

        services.AddSingleton<ILoggerFactory>(new XUnitLoggerFactory(output, minimumLevel));

        return services;
    }
}