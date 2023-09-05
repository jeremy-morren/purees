using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using PureES.Core;
using PureES.Core.EventStore;
using PureES.EventStore.InMemory;
using PureES.EventStore.InMemory.Serialization;
using PureES.EventStoreDB;

namespace PureES.EventStores.Tests;

internal static class TestSerializer
{
    public static readonly BasicEventTypeMap EventTypeMap = new(
        new OptionsWrapper<PureESOptions>(
            new PureESOptions()
            {
                Assemblies = { typeof(TestSerializer).Assembly }
            }));

    public static InMemoryEventStoreSerializer InMemoryEventStoreSerializer =>
        new (EventTypeMap, new OptionsWrapper<InMemoryEventStoreOptions>(new InMemoryEventStoreOptions()));

    public static EventStoreDBSerializer EventStoreDBSerializer =>
        new(EventTypeMap, new OptionsWrapper<EventStoreDBOptions>(new EventStoreDBOptions()));
}