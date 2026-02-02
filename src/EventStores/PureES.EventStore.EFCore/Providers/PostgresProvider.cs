using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Providers;

internal class PostgresProvider(EventStoreDbContext context) : IEfCoreProvider
{
    public void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder)
    {
        builder.Property(e => e.Timestamp)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("transaction_timestamp()");
    }

    public Task<List<EventStoreEvent>> WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        return context.WriteAndSaveChanges(events.ToList(), ct);
    }

    public bool IsUniqueConstraintFailedException(DbException e)
    {
        return e.SqlState == "23505" && e.GetType().FullName == "Npgsql.PostgresException";
    }
}