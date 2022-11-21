using System.Text.Json;
using PureES.Core.EventStore.Serialization;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStoreDB.Serialization;

namespace PureES.Extensions.Tests.EventStore;

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
        EventTypeMap.AddType(typeof(EventStoreTestsBase.Event));
    }

    public static IInMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new InMemoryEventStoreSerializer<object>(EventTypeMap, Serializer);

    public static IEventStoreDBSerializer EventStoreDBSerializer =>
        new EventStoreDBSerializer<object>(EventTypeMap, Serializer);
}