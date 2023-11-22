namespace PureES.EventStores.CosmosDB;

internal record StreamPosition(string EventStreamId, ulong Position);