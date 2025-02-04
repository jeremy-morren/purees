using System.Collections;

namespace PureES;

/// <summary>
/// A map of multiple event streams to be committed to the database in a single transaction.
/// </summary>
[PublicAPI]
public class EventsTransaction : IEventsTransaction
{
    private record Stream(string Id, EventsList Events);

    private readonly List<Stream> _events = [];

    /// <inheritdoc />
    public void Add(string streamId, uint? revision, IEnumerable<object> events)
    {
        Add(streamId, new EventsList(revision, events));
    }
    
    /// <inheritdoc />
    public void Add(string streamId, uint? revision, params object[] events)
    {
        Add(streamId, new EventsList(revision, events));
    }
    
    /// <inheritdoc />
    public void Add<T>(string streamId, uint? revision, T[] events)
    {
        Add(streamId, new EventsList(revision, events.Cast<object>()));
    }

    /// <inheritdoc />
    public void AddOrAppend(string streamId, uint? revision, params object[] events)
    {
        var current = _events.Where(e => e.Id == streamId).Select(e => e.Events).FirstOrDefault();
        if (current != null)
        {
            if (current.ExpectedRevision != revision)
                throw new InvalidOperationException("Expected revision mismatch.");
            current.AddRange(events);
        }
        else
        {
            Add(streamId, revision, events);
        }
    }
    
    /// <inheritdoc />
    public void Add(string streamId, EventsList events)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(events);

        if (_events.Any(e => e.Id == streamId))
            throw new InvalidOperationException($"Stream '{streamId}' already exists in the transaction.");
        _events.Add(new Stream(streamId, events));
    }

    /// <inheritdoc />
    public EventsList this[string key] =>
        _events.Where(e => e.Id == key).Select(e => e.Events).SingleOrDefault()
        ?? throw new KeyNotFoundException($"Stream '{key}' not found in transaction");

    /// <inheritdoc />
    public void Clear() => _events.Clear();

    /// <inheritdoc />
    public int Count => _events.Count;

    /// <inheritdoc />
    public bool ContainsStream(string streamId)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        return _events.Any(e => e.Id == streamId);
    }

    /// <inheritdoc />
    public bool Remove(string streamId)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        var index = _events.FindIndex(e => e.Id == streamId);
        if (index == -1) return false;
        _events.RemoveAt(index);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetStream(string streamId, [MaybeNullWhen(false)] out EventsList value)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        var stream = _events.Where(e => e.Id == streamId).Select(e => e.Events).SingleOrDefault();
        if (stream != null)
        {
            value = stream;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> StreamIds => _events.Select(e => e.Id).ToList();

    /// <inheritdoc />
    [MustDisposeResource]
    public IEnumerator<KeyValuePair<string, EventsList>> GetEnumerator() =>
        _events.Select(e => new KeyValuePair<string, EventsList>(e.Id, e.Events)).GetEnumerator();

    /// <inheritdoc />
    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"Count = {Count}";
}