using System.Data.Common;
using System.Globalization;
using System.Text.Json;
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

        builder.OwnsMany(x => x.EventTypes)
            .HasKey("Id"); //See https://stackoverflow.com/a/69826156/6614154
        
        var jsonOpts = JsonSerializerOptions.Default;

        builder.Property(e => e.Event)
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
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once IdentifierTypo
        const int SQLITE_CONSTRAINT_PRIMARYKEY = 1555;
        
        return e.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" 
               && ((dynamic)e).SqliteExtendedErrorCode == SQLITE_CONSTRAINT_PRIMARYKEY;
    }

    public DateTime ReadTimestamp(DbDataReader reader, int ordinal) =>
        UtcDateConverter.Parse(reader.GetString(ordinal)).UtcDateTime;

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
            DateTime.ParseExact(s, "O", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Formats the date as utc
        /// </summary>
        private static string Format(DateTimeOffset dt) =>
            dt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }
    
    #endregion
}