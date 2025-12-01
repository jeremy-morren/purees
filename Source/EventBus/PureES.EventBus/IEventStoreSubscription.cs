using Microsoft.Extensions.Hosting;

namespace PureES.EventBus;

/// <summary>
///     Tag interface for identifying a subscription to an EventStore
/// </summary>
public interface IEventStoreSubscription : IHostedService;