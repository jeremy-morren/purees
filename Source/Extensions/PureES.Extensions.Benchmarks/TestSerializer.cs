using System.Text.Json;
using System.Text.Json.Nodes;
using PureES.Core.EventStore.Serialization;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStoreDB.Serialization;

namespace PureES.Extensions.Benchmarks;

internal static class TestSerializer
{
    static TestSerializer()
    {
        EventTypeMap.AddType(typeof(JsonObject));
    }

    public static readonly BasicEventTypeMap EventTypeMap = new();

    private static readonly IEventStoreSerializer Serializer = new JsonEventStoreSerializer(
        new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    public static IInMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new InMemoryEventStoreSerializer<object>(EventTypeMap, Serializer);
}