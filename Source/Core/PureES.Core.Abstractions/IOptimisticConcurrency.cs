namespace PureES.Core;

public interface IOptimisticConcurrency
{
    ValueTask<ulong?> GetExpectedRevision(object command, CancellationToken ct);
}