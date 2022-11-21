using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using PureES.Core.EventStore;

namespace PureES.EventStore.InMemory;

public static class InMemoryEventStoreServiceCollectionExtensions
{
    /// <summary>
    ///     Adds an <see cref="IEventStore" /> implementation
    ///     that persists events in Memory.
    /// </summary>
    /// <param name="services"></param>
    /// <returns>The service collection, so that further calls can be changed</returns>
    /// <remarks>
    ///     Any existing registrations of <see cref="IEventStore" /> will not be overwritten,
    ///     hence this method is safe to be called multiple times
    /// </remarks>
    public static IServiceCollection AddInMemoryEventStore(this IServiceCollection services)
    {
        services.TryAddSingleton<ISystemClock, SystemClock>();

        services.TryAddSingleton<IInMemoryEventStore, InMemoryEventStore>();

        services.TryAddTransient<IEventStore>(sp => sp.GetRequiredService<IInMemoryEventStore>());

        return services;
    }
}