using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStoreDB;

namespace PureES.EventStores.Tests;

internal static class TestSerializer
{
    public static InMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new (new BasicEventTypeMap(), new OptionsWrapper<InMemoryEventStoreOptions>(new InMemoryEventStoreOptions()));

    public static EventStoreDBSerializer EventStoreDBSerializer =>
        new(new BasicEventTypeMap(), new OptionsWrapper<EventStoreDBOptions>(new EventStoreDBOptions()));
}