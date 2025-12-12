namespace PureES;

/// <summary>
/// Enriches an event before persisting
/// </summary>
public interface IAsyncEventEnricher
{
    /// <summary>
    /// Enriches an event before persisting
    /// </summary>
    Task Enrich(UncommittedEvent @event, CancellationToken ct);
}