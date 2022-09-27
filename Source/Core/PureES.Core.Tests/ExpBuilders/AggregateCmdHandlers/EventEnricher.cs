using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class EventEnricher : IEventEnricher
{
    private readonly Func<object, object, object?> _getMetadata;

    public EventEnricher(Func<object, object, object?> getMetadata) => _getMetadata = getMetadata;
    
    public ValueTask<object?> GetMetadata(object command, object @event, CancellationToken ct) => 
        ValueTask.FromResult(_getMetadata(command, @event));
}

public static class EventEnricherExtensions
{
    public static IServiceCollection AddEventEnricher(this IServiceCollection services,
        Func<object, object, object?> getMetadata)
        => services.AddSingleton<IEventEnricher>(new EventEnricher(getMetadata));
}