using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Serialization;

namespace PureES.Extensions.Benchmarks;

internal static class TestSerializer
{
    public static readonly BasicEventTypeMap EventTypeMap = new();

    static TestSerializer()
    {
        EventTypeMap.AddType(typeof(JsonObject));
    }

    public static InMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new (EventTypeMap, 
            new OptionsWrapper<InMemoryEventStoreOptions>(new InMemoryEventStoreOptions()));
}