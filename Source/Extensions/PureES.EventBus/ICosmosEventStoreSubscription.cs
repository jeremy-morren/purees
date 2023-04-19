using Microsoft.Extensions.Hosting;

namespace PureES.EventBus;

/// <summary>
///     Tag interface for identifying CosmosDB EventStore subscriptions
/// </summary>
public interface IEventStoreSubscription : IHostedService
{
}