namespace PureES;

/// <summary>
/// A map of multiple event streams to be committed to the database in a single transaction.
/// </summary>
public interface IEventsTransaction : IDictionary<string, EventsList>;