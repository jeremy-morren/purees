using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Providers;

internal class SqliteProvider(EventStoreDbContext context) : IEfCoreProvider
{
    public void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder)
    {
        builder.Property(e => e.Timestamp)
            .HasConversion(new UtcDateConverter());
        
        var jsonOpts = JsonSerializerOptions.Default;

        builder.Property(e => e.EventTypes)
            .HasConversion(
                x => JsonSerializer.Serialize(x, jsonOpts),
                x => JsonSerializer.Deserialize<ImmutableArray<string>>(x, jsonOpts))
            .IsRequired();

        builder.Property(e => e.Data)
            .HasConversion(
                x => JsonSerializer.Serialize(x, jsonOpts),
                x => JsonSerializer.Deserialize<JsonElement>(x, jsonOpts))
            .IsRequired();
        
        builder.Property(e => e.Metadata)
            .HasConversion(
                x => x != null ? JsonSerializer.Serialize(x, jsonOpts) : null,
                x => x != null ? JsonSerializer.Deserialize<JsonElement>(x, jsonOpts) : null);
    }

    public Task<List<EventStoreEvent>> WriteEvents(IEnumerable<EventStoreEvent> events, CancellationToken ct)
    {
        var ts = DateTimeOffset.Now;
        var list = events.ToList();
        foreach (var e in list)
            e.Timestamp = ts;

        return context.WriteAndSaveChanges(list, ct);
    }
    
    public bool IsUniqueConstraintFailedException(DbException e)
    {
        const int errorCode = 1555; // UNIQUE constraint failed
        return e.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" 
               && ((dynamic)e).SqliteExtendedErrorCode == errorCode;
    }

    private static IQueryable<object> SelectColumns(IQueryable<EventStoreEvent> query) =>
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

    public IAsyncEnumerable<EventEnvelope> ReadEvents(
        IQueryable<EventStoreEvent> queryable,
        EfCoreEventSerializer serializer,
        CancellationToken ct)
    {
        var command = SelectColumns(queryable).CreateDbCommand();
        return ReadEvents(command, serializer, ct);
    }
    
    public async IAsyncEnumerable<EventEnvelope> ReadEvents(
        DbCommand command,
        EfCoreEventSerializer serializer, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using (command)
        {
            //Columns from SelectColumns
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
    }


    #region Queryable Helpers

    public IQueryable<long> CountByEventType(List<string> eventTypes)
    {
        var table = GetEventEntity().GetTableName()!;
        var sql = $"SELECT COUNT(1) as value FROM \"{table}\" {CreateEventTypeFilter()}";
        return context.Database.SqlQueryRaw<long>(sql, JsonSerializer.Serialize(eventTypes));
    }

    public DbCommand FilterByEventType(IQueryable<EventStoreEvent> query, List<string> eventTypes, ulong? maxCount)
    {
        var command = SelectColumns(query).CreateDbCommand();
        
        //Insert the filter before order by
        var index = command.CommandText.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine(command.CommandText[..index]);
        sb.AppendLine(CreateEventTypeFilter());
        sb.AppendLine(command.CommandText[index..]);

        if (maxCount.HasValue)
        {
            sb.AppendLine("LIMIT @p1");
            AddParameter("@p1", maxCount.Value);
        }

        command.CommandText = sb.ToString();
        
        AddParameter("@p0", JsonSerializer.Serialize(eventTypes));

        return command;
        
        void AddParameter(string name, object value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            command.Parameters.Add(p);
        }
    }

    private string CreateEventTypeFilter()
    {
        var eventEntity = GetEventEntity();
        var column = eventEntity.FindProperty(nameof(EventStoreEvent.EventTypes))!.GetColumnName();
        return $" WHERE EXISTS (SELECT 1 FROM json_each(\"{column}\") WHERE value IN (select value from json_each(@p0)))";
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