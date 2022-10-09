namespace PureES.Core;

public record LoadedAggregate<T>(T Aggregate, ulong Version)
{
    /// <summary>
    /// Gets the built <c>Aggregate</c>
    /// </summary>
    public T Aggregate { get; init; } = Aggregate;
    
    /// <summary>
    /// Gets the number of events used to build <see cref="Aggregate"/>
    /// </summary>
    public ulong Version { get; init; } = Version;
}