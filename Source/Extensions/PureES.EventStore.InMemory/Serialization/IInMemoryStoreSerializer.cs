using PureES.Core;
using PureES.Core.EventStore;

namespace PureES.EventStore.InMemory.Serialization;

internal interface IInMemoryEventStoreSerializer
{
    EventRecord Serialize(UncommittedEvent record, string streamId, DateTimeOffset created);

    EventEnvelope Deserialize(EventRecord record);
}