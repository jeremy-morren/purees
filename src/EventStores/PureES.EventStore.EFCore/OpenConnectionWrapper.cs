using System.Data;
using System.Data.Common;

namespace PureES.EventStore.EFCore;

/// <summary>
/// A wrapper around a <see cref="DbConnection"/> that closes the connection when disposed, if it was opened by the wrapper
/// </summary>
/// <remarks>
/// The connection used by DbContext should not be disposed by the caller, as it is managed by the DbContext.
/// However, if we open the connection then we must close it to return it to the connection pool.
/// </remarks>
internal class OpenConnectionWrapper : IAsyncDisposable
{
    private readonly DbConnection _connection;

    private OpenConnectionWrapper(DbConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Opens the connection if it is not already open
    /// </summary>
    /// <returns>
    /// A wrapper around the connection that will close it when disposed, or null if the connection was already open
    /// </returns>
    public static async Task<OpenConnectionWrapper?> OpenAsync(DbCommand command, CancellationToken ct)
    {
        var connection = command.Connection;
        if (connection == null || connection.State == ConnectionState.Open)
            return null;
        await connection.OpenAsync(ct);
        return new OpenConnectionWrapper(connection);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
    }
}