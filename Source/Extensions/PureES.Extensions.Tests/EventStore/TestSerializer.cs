using Microsoft.Extensions.Options;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStoreDB;

namespace PureES.Extensions.Tests.EventStore;

internal static class TestSerializer
{
    public static readonly BasicEventTypeMap EventTypeMap = new();

    static TestSerializer()
    {
        EventTypeMap.AddType(typeof(EventStoreTestsBase.Event));
    }

    public static InMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new (EventTypeMap, new OptionsWrapper<InMemoryEventStoreOptions>(new InMemoryEventStoreOptions()));

    public static EventStoreDBSerializer EventStoreDBSerializer =>
        new(EventTypeMap, new OptionsWrapper<EventStoreDBOptions>(new EventStoreDBOptions()));
}