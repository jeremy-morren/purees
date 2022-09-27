using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class OptimisticConcurrency : IOptimisticConcurrency
{
    private readonly Func<object, ulong?> _getExpectedVersion;

    public OptimisticConcurrency(Func<object, ulong?> getExpectedVersion) => _getExpectedVersion = getExpectedVersion;

    public ValueTask<ulong?> GetExpectedVersion(object command, CancellationToken ct) =>
        ValueTask.FromResult(_getExpectedVersion(command));
}

public static class OptimisticConcurrencyServiceExtensions
{
    public static IServiceCollection AddOptimisticConcurrency(this IServiceCollection services,
        Func<object, ulong?> getExpectedVersion) =>
        services.AddSingleton<IOptimisticConcurrency>(new OptimisticConcurrency(getExpectedVersion));
}