namespace PureES.Core;

public interface IOptimisticConcurrency
{
    ValueTask<ulong?> GetExpectedVersion(object command, CancellationToken ct);
}