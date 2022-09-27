namespace PureES.Core;

public record LoadedAggregate<T>(T Aggregate, ulong Revision);