using System.Text.Json.Serialization;
using ProtoBuf;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

#pragma warning disable CS8618

namespace PureES.EventStore.InMemory.Serialization;

[JsonSerializable(typeof(EventRecord))]
[ProtoContract]
internal class EventRecord
{
    //ProtoMember(1) is reserved for length
    
    [ProtoMember(2)] [JsonInclude] public string StreamId;
    [ProtoMember(4)] [JsonInclude] public DateTime Created;
    [ProtoMember(5)] [JsonInclude] public byte[] Event;
    [ProtoMember(6)] [JsonInclude] public Guid EventId;
    [ProtoMember(7)] [JsonInclude] public string EventType;
    [ProtoMember(8)] [JsonInclude] public byte[]? Metadata;

    [JsonIgnore] [ProtoIgnore] public uint StreamPosition;
}