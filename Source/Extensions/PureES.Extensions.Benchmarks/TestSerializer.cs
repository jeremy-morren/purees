using System.Text.Json;
using System.Text.Json.Nodes;
using PureES.Core.EventStore.Serialization;
using PureES.EventStore.InMemory.Serialization;

namespace PureES.Extensions.Benchmarks;

internal static class TestSerializer
{
    public static readonly BasicEventTypeMap EventTypeMap = new();

    private static readonly IEventStoreSerializer Serializer = new JsonEventStoreSerializer(
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    static TestSerializer()
    {
        EventTypeMap.AddType(typeof(JsonObject));
    }

    public static IInMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new InMemoryEventStoreSerializer<object>(EventTypeMap, Serializer);
}