using EventStore.Client;
using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStoreDB.Serialization;

internal interface IEventStoreDBSerializer
{
    EventEnvelope Deserialize(EventRecord record);

    EventData Serialize(UncommittedEvent record);
}