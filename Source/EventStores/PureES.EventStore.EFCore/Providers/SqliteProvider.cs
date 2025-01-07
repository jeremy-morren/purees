using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PureES.EventStore.EFCore.Providers;

internal class SqliteProvider(EventStoreDbContext context) : IEfCoreProvider
{
    public void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder)
    {
        builder.Property(e => e.Timestamp)
            .HasConversion(new UtcDateConverter());
        
        var jsonOpts = JsonSerializerOptions.Default;

        builder.Property(e => e.EventTypes)
            .HasConversion(t => JsonSerializer.Serialize(t, jsonOpts),
                s => JsonSerializer.Deserialize<ImmutableArray<string>>(s, jsonOpts))
            .IsRequired();

        builder.Property(e => e.Data)
            .HasConversion(
                e => JsonSerializer.Serialize(e, jsonOpts),
                s => JsonSerializer.Deserialize<JsonElement>(s, jsonOpts))
            .IsRequired();
        
        builder.Property(e => e.Metadata)
            .HasConversion(e => e != null ? JsonSerializer.Serialize(e, jsonOpts) : null,
                s => s != null ? JsonSerializer.Deserialize<JsonElement>(s, jsonOpts) : null);
    }

    public Task<List<EventStoreEvent>> WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        var ts = DateTimeOffset.Now;
        var list = events.ToList();
        foreach (var e in list)
            e.Timestamp = ts;

        return context.WriteAndSaveChanges(list, ct);
    }

    public async IAsyncEnumerable<EventEnvelope> ReadEvents(
        IQueryable<EventStoreEvent> query, 
        EfCoreEventSerializer serializer, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var q =
            from x in query
            select new
            {
                x.StreamId,
                x.StreamPos,
                x.Timestamp,
                x.EventType,
                x.Data,
                x.Metadata
            };
        await using var command = q.CreateDbCommand();
        await using var reader = await command.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var streamId = reader.GetString(0);
            var streamPos = reader.GetInt32(1);
            var timestamp = UtcDateConverter.Parse(reader.GetString(2));
            var eventType = reader.GetString(3);
            var data = reader.GetString(4);
            var metadata = reader.IsDBNull(5) ? null : reader.GetString(5);
            yield return new EventEnvelope(streamId, 
                (ulong)streamPos, 
                timestamp.UtcDateTime, 
                serializer.DeserializeEvent(streamId, streamPos, eventType, data),
                serializer.DeserializeMetadata(streamId, streamPos, metadata));
        }
    }

    public bool IsUniqueConstraintFailedException(DbException e)
    {
        const int errorCode = 1555; // UNIQUE constraint failed
        return e.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" 
               && ((dynamic)e).SqliteExtendedErrorCode == errorCode;
    }
    
    #region Queryable Helpers

    public IQueryable<long> CountByEventType(List<string> eventTypes)
    {
        var eventEntity = GetEventEntity();
        var table = eventEntity.GetTableName()!;
        var property = eventEntity.FindProperty(nameof(EventStoreEvent.EventTypes))!;
        var column = property.GetColumnName();
        
        var sql = $"SELECT COUNT(1) as value FROM [{table}] WHERE EXISTS (SELECT 1 FROM json_each([{column}]) WHERE value IN (select value from json_each(@p0)))";
        return context.Database.SqlQueryRaw<long>(sql, JsonSerializer.Serialize(eventTypes));
    }
    
    public IQueryable<EventStoreEvent> ReadMany(List<string> streamIds)
    {
        var eventEntity = GetEventEntity();
        var table = eventEntity.GetTableName()!;
        var streamId = eventEntity.FindProperty(nameof(EventStoreEvent.StreamId))!;
        var column = streamId.GetColumnName();
        
        var sql = $"SELECT * FROM [{table}] t INNER JOIN (select value from json_each(@p0)) as ids ON t.[{column}] = ids.value";
        return context.Set<EventStoreEvent>().FromSqlRaw(sql, JsonSerializer.Serialize(streamIds));
    }

    private IEntityType GetEventEntity() => context.Model.FindEntityType(typeof(EventStoreEvent))!;
    
    #endregion
    
    #region Converters

    private class UtcDateConverter : ValueConverter<DateTimeOffset, string>
    {
        public UtcDateConverter(ConverterMappingHints? mappingHints = null) 
            : base(
                x => Format(x),
                x => Parse(x), 
                mappingHints)
        {
        }

        public static DateTimeOffset Parse(string s) => 
            DateTimeOffset.ParseExact(s, "O", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Formats the date as utc
        /// </summary>
        private static string Format(DateTimeOffset dt) =>
            dt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }
    
    #endregion
}