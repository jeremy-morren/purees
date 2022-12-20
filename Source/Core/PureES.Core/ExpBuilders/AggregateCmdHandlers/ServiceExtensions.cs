namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal static class ServiceExtensions
{
    public static async ValueTask<object?> GetMetadata(this IServiceProvider provider,
        object command,
        object @event,
        CancellationToken ct)
    {
        var src = provider.GetService<IEventEnricher>();
        return src != null ? await src.GetMetadata(command, @event, ct) : null;
    }

    public static async ValueTask<ulong?> GetExpectedRevision(this IServiceProvider provider,
        object command,
        CancellationToken ct)
    {
        var svc = provider.GetService<IOptimisticConcurrency>();
        return svc != null ? await svc.GetExpectedRevision(command, ct) : null;
    }
}