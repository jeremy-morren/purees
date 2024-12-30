namespace PureES.EventStore.CosmosDB;

internal record StreamPosition(string EventStreamId, ulong Position);