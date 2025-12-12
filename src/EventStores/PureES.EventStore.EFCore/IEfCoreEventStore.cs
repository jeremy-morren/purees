namespace PureES.EventStore.EFCore;

/// <summary>
/// An event store using EFCore
/// </summary>
public interface IEfCoreEventStore : IEventStore
{
    /// <summary>
    /// Generates an idempotent sql script to create the event store schema
    /// </summary>
    string GenerateIdempotentCreateScript();
}