using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using PureES.EventStore.EFCore.Models;

namespace PureES.EventStore.EFCore.Providers;

internal class SqliteProvider(EventStoreDbContext context) : IEfCoreProvider
{
    public void ConfigureEntity(EntityTypeBuilder<EventStoreEvent> builder)
    {
        builder.Property(e => e.Timestamp)
            // SQLite does not have a native datetime type, so we store it as INTEGER (int64) as ticks since Unix epoch
            .HasColumnType("INTEGER") 
            // Store
            .HasConversion(
                instant => instant.ToUnixTimeTicks(),
                ticks => Instant.FromUnixTimeTicks(ticks));

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
        // Set all events to a single timestamp
        var ts = SystemClock.Instance.GetCurrentInstant();
        
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