using Microsoft.Extensions.DependencyInjection;
using PureES.Core;

namespace PureES.EventBus.EventHandlers;

public static class EventHandlerServiceCollectionExtensions
{
    public static IServiceCollection On<TEvent, TMetadata>(this IServiceCollection services,
        Func<EventEnvelope<TEvent, TMetadata>, IServiceProvider, CancellationToken, Task> @delegate)
        where TEvent : notnull
        where TMetadata : notnull
    {
        services.AddSingleton<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>(sp =>
            new AsyncDelegateEventHandler<TEvent, TMetadata>(sp, @delegate));

        return services;
    }
    
    public static IServiceCollection On<TEvent, TMetadata>(this IServiceCollection services,
        Action<EventEnvelope<TEvent, TMetadata>, IServiceProvider> @delegate)
        where TEvent : notnull
        where TMetadata : notnull
    {
        services.AddSingleton<Func<IServiceProvider, IEventHandler<TEvent, TMetadata>>>(sp =>
            new DelegateEventHandler<TEvent, TMetadata>(sp, @delegate));

        return services;
    }
}