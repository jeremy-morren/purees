using System.Collections;

namespace PureES.Core;

/// <summary>
/// A map of multiple event streams to be committed to the database in a single transaction.
/// </summary>
[PublicAPI]
public class EventsTransaction : IDictionary<string, EventsList>
{
    private readonly IDictionary<string, EventsList> _dictionary = new Dictionary<string, EventsList>();

    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="revision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Stream events.</param>
    public void Add(string streamId, ulong? revision, IEnumerable<object> @events)
    {
        Add(streamId, new EventsList(revision, events));
    }
    
    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="revision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Stream events.</param>
    public void Add(string streamId, ulong? revision, params object[] @events)
    {
        Add(streamId, new EventsList(revision, events));
    }
    
    /// <summary>
    /// Adds a new stream to the transaction.
    /// </summary>
    /// <param name="streamId">The event stream id</param>
    /// <param name="value">Stream events list.</param>
    public void Add(string streamId, EventsList value)
    {
        if (streamId == null) throw new ArgumentNullException(nameof(streamId));
        if (value == null) throw new ArgumentNullException(nameof(value));
        _dictionary.Add(streamId, value);
    }
    
    public void Add(KeyValuePair<string, EventsList> item)
    {
        Add(item.Key, item.Value);
    }
    
    public EventsList this[string key]
    {
        get => _dictionary[key];
        set
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            _dictionary[key] = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public void Clear() => _dictionary.Clear();

    public bool Contains(KeyValuePair<string, EventsList> item) => _dictionary.Contains(item);

    public bool Remove(KeyValuePair<string, EventsList> item) => _dictionary.Remove(item);

    public int Count => _dictionary.Count;

    public bool IsReadOnly => false;

    public bool ContainsKey(string key) => _dictionary.ContainsKey(key);

    public bool Remove(string key) => _dictionary.Remove(key);

    public bool TryGetValue(string key, out EventsList value) => _dictionary.TryGetValue(key, out value);

    public ICollection<string> Keys => _dictionary.Keys;

    public ICollection<EventsList> Values => _dictionary.Values;

    public void CopyTo(KeyValuePair<string, EventsList>[] array, int arrayIndex) => _dictionary.CopyTo(array, arrayIndex);
    
    public IEnumerator<KeyValuePair<string, EventsList>> GetEnumerator() => _dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dictionary).GetEnumerator();

    public override string ToString() => $"Count = {Count}";
    
    public IReadOnlyDictionary<string, UncommittedEventsList> ToUncommittedTransaction() =>
        _dictionary.ToDictionary(p => p.Key, 
            p => new UncommittedEventsList(p.Value.ExpectedRevision, p.Value));
}