namespace PureES.Core;

public sealed record UncommittedEvent(Guid EventId, object Event, object? Metadata);