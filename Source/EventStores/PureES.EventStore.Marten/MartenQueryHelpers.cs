using System.Data.Common;
using System.Runtime.CompilerServices;
using Marten;
using Npgsql;
using Weasel.Core;

namespace PureES.EventStore.Marten;

internal static class MartenQueryHelpers
{
    public static DbObjectName GetTableName(this IDocumentStore store, Type type)
    {
        return store.Options.FindOrResolveDocumentType(type).TableName;
    }
    public static async IAsyncEnumerable<TResult> QueryRaw<TResult>(this IQuerySession session,
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        Func<DbDataReader, TResult> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var command = new NpgsqlCommand(sql);
        foreach (var (name, value) in parameters)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            command.Parameters.Add(p);
        }
        await using var reader = await session.ExecuteReaderAsync(command, ct);
        while (await reader.ReadAsync(ct))
            yield return selector(reader);
    }
}