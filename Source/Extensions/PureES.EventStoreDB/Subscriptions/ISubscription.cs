using Microsoft.Extensions.Hosting;

namespace PureES.EventStoreDB.Subscriptions;

/// <summary>
/// Tag interface for identifying EventStore subscriptions
/// </summary>
public interface ISubscription : IHostedService
{
    
}