using System.Text;
using System.Text.Json;
using EventStore.Client;

namespace PureES.EventStoreDB.Subscriptions;

public class EventStoreSubscriptionCheckpointRepository: ISubscriptionCheckpointRepository
{
    private readonly global::EventStore.Client.EventStoreClient _eventStoreClient;

    public EventStoreSubscriptionCheckpointRepository(global::EventStore.Client.EventStoreClient eventStoreClient) => _eventStoreClient = eventStoreClient;

    public async ValueTask<ulong?> Load(string subscriptionId, CancellationToken ct)
    {
        var streamName = GetCheckpointStreamName(subscriptionId);

        var result = _eventStoreClient.ReadStreamAsync(Direction.Backwards, 
            streamName, 
            StreamPosition.End, 
            1,
            cancellationToken: ct);

        if (await result.ReadState == ReadState.StreamNotFound)
            return null;

        return await result
            .Select(r => CheckpointStored.DeSerialize(r.Event)?.Position)
            .FirstOrDefaultAsync(ct);
    }

    public async ValueTask Store(string subscriptionId, ulong position, CancellationToken ct)
    {
        var @event = new CheckpointStored(subscriptionId, position, DateTime.UtcNow);
        var data = new[] {@event.CreateEvent()};
        var streamName = GetCheckpointStreamName(subscriptionId);

        try
        {
            // store new checkpoint expecting stream to exist
            await _eventStoreClient.AppendToStreamAsync(
                streamName,
                StreamState.StreamExists,
                data,
                cancellationToken: ct
            );
        }
        catch (WrongExpectedVersionException)
        {
            // WrongExpectedVersionException means that stream did not exist
            // Set the checkpoint stream to have at most 1 event
            // using stream metadata $maxCount property
            await _eventStoreClient.SetStreamMetadataAsync(
                streamName,
                StreamState.NoStream,
                new StreamMetadata(1),
                cancellationToken: ct
            );

            // append event again expecting stream to not exist
            await _eventStoreClient.AppendToStreamAsync(
                streamName,
                StreamState.NoStream,
                data,
                cancellationToken: ct
            );
        }
    }

    private static string GetCheckpointStreamName(string subscriptionId) => $"checkpoint_{subscriptionId}";

    public record CheckpointStored(string SubscriptionId, ulong? Position, DateTime CheckPointedAt)
    {
        public EventData CreateEvent() => new(Uuid.NewUuid(),
            nameof(CheckpointStored),
            JsonSerializer.SerializeToUtf8Bytes(this));

        public static CheckpointStored? DeSerialize(EventRecord source) =>
            JsonSerializer.Deserialize<CheckpointStored>(source.Data.Span);
    }

    public static bool IsCheckpointEvent(EventRecord record) => record.EventType == nameof(CheckpointStored);
}
