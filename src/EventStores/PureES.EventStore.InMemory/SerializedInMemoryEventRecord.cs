using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace PureES.EventStore.InMemory;

/// <summary>
/// A serialized event record for in-memory event store
/// </summary>
[PublicAPI]
public readonly struct SerializedInMemoryEventRecord
{
    internal readonly InMemoryEventRecord Source;

    internal SerializedInMemoryEventRecord(InMemoryEventRecord source)
    {
        Source = source;
    }

    [JsonConstructor]
    public SerializedInMemoryEventRecord(
        int streamPos,
        string streamId,
        DateTime timestamp,
        ImmutableArray<string> eventTypes,
        JsonElement @event,
        JsonElement? metadata)
    {
        Source = new InMemoryEventRecord()
        {
            StreamPos = streamPos,
            StreamId = streamId,
            Timestamp = timestamp,
            EventTypes = eventTypes,
            Event = @event,
            Metadata = metadata
        };
    }

    /// <inheritdoc cref="InMemoryEventRecord.StreamPos"/>
    public int StreamPos => Source.StreamPos;

    /// <inheritdoc cref="InMemoryEventRecord.StreamId"/>
    public string StreamId => Source.StreamId;

    /// <inheritdoc cref="InMemoryEventRecord.Timestamp"/>
    public DateTime Timestamp => Source.Timestamp;

    /// <inheritdoc cref="InMemoryEventRecord.EventTypes"/>
    public ImmutableArray<string> EventTypes => Source.EventTypes;

    /// <inheritdoc cref="InMemoryEventRecord.Event"/>
    public JsonElement Event => Source.Event;

    /// <inheritdoc cref="InMemoryEventRecord.Metadata"/>
    public JsonElement? Metadata => Source.Metadata;
}

/// <summary>
/// JSON context for serializing/deserializing <see cref="SerializedInMemoryEventRecord"/>
/// </summary>
[JsonSerializable(typeof(IEnumerable<SerializedInMemoryEventRecord>))]
[JsonSerializable(typeof(IReadOnlyList<SerializedInMemoryEventRecord>))]
[JsonSerializable(typeof(List<SerializedInMemoryEventRecord>))]
public partial class InMemoryEventStoreJsonSerializerContext : JsonSerializerContext
{

}