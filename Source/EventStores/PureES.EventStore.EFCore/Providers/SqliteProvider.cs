using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PureES.EventStore.EFCore.Providers;

internal class SqliteProvider(EventStoreDbContext context) : IEfCoreProvider
{
    public void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder)
    {
        builder.Property(e => e.Timestamp)
            .HasConversion(new UtcDateConverter());
        
        builder.Property(e => e.Data)
            .HasConversion(new JsonElementConverter());
        builder.Property(e => e.Metadata)
            .HasConversion(new JsonElementNullConverter());
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
            var timestamp = ParseDateTime(reader.GetString(2));
            var eventType = reader.GetString(3);
            var data = reader.GetString(4);
            var metadata = reader.IsDBNull(5) ? null : reader.GetString(5);
            yield return new EventEnvelope(streamId, 
                (ulong)streamPos, 
                timestamp, 
                serializer.DeserializeEvent(streamId, streamPos, eventType, data),
                serializer.DeserializeMetadata(streamId, streamPos, metadata));
        }
    }
    
    private static DateTime ParseDateTime(string s)
    {
        return DateTime.ParseExact(s, "O", null, DateTimeStyles.RoundtripKind);
    }

    public bool IsUniqueConstraintFailedException(DbException e)
    {
        return e.Source == "Microsoft.Data.Sqlite" && ((dynamic)e).SqliteExtendedErrorCode == 1555;
    }
    
    private class JsonElementConverter : ValueConverter<JsonElement, string>
    {
        public JsonElementConverter(ConverterMappingHints? mappingHints = null) 
            : base(
                e => JsonSerializer.Serialize(e, JsonSerializerOptions.Default),
                s => JsonSerializer.Deserialize<JsonElement>(s, JsonSerializerOptions.Default),
                mappingHints)
        {
        }
    }
    private class JsonElementNullConverter : ValueConverter<JsonElement?, string?>
    {
        public JsonElementNullConverter(ConverterMappingHints? mappingHints = null) 
            : base(
                e => JsonSerializer.Serialize(e, JsonSerializerOptions.Default),
                s => s != null ? JsonSerializer.Deserialize<JsonElement?>(s, JsonSerializerOptions.Default) : null,
                mappingHints)
        {
        }
    }

    private class UtcDateConverter : ValueConverter<DateTimeOffset, string>
    {
        public UtcDateConverter(ConverterMappingHints? mappingHints = null) 
            : base(
                e => e.UtcDateTime.ToString("O"),
                s => DateTime.ParseExact(s, "O", null),
                mappingHints)
        {
        }
    }
}