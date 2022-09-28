using EventStore.Client;
using PureES.Core;

namespace PureES.EventStoreDB.Serialization;

public interface IEventStoreDBSerializer
{
    string GetTypeName(Type eventType);
    
    EventEnvelope Deserialize(EventRecord record);
    EventData Serialize(UncommittedEvent @event);

    EventData Serialize<T>(T @event);
    T Deserialize<T>(EventRecord record);
}