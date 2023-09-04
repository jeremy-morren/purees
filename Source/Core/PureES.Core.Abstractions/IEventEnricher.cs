namespace PureES.Core;

/// <summary>
/// Enriches an event before persisting
/// </summary>
public interface IEventEnricher
{
    /// <summary>
    /// Enriches an event before persisting
    /// </summary>
    void Enrich(UncommittedEvent @event);
}