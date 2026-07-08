using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Providers;

internal class PostgresProvider(EventStoreDbContext context) : IEfCoreProvider
{
    public void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder)
    {
        // Timestamp: column type is timestamp with time zone, materialized type is DateTime with kind Utc
        // Default sql is transaction timestamp
        // See https://www.npgsql.org/doc/types/datetime.html
        builder.Property(e => e.Timestamp)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("transaction_timestamp()")
            .HasConversion(
                instant =>  instant.ToDateTimeUtc(),
                dateTime => Instant.FromDateTimeUtc(dateTime));
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