using EventStore.Client;
using PureES.Core;

namespace PureES.EventStoreDB;

public interface IEventStoreDBSerializer
{
    string GetTypeName(Type eventType);
    
    EventEnvelope DeSerialize(EventRecord record);
    EventData Serialize(UncommittedEvent @event);

    EventData Serialize<T>(T @event);
    T DeSerialize<T>(EventRecord record);
}