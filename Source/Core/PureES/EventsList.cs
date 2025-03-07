﻿using System.Collections;

namespace PureES;

/// <summary>
/// A list of events to be appended to an event stream.
/// </summary>
[PublicAPI]
public class EventsList : IList<object>
{
    private readonly List<object> _events = [];
    
    /// <summary>
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </summary>
    public uint? ExpectedRevision { get; set; }

    /// <summary>
    /// Creates a new <see cref="EventsList" />.
    /// </summary>
    /// <param name="expectedRevision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    public EventsList(uint? expectedRevision)
    {
        ExpectedRevision = expectedRevision;
    }

    /// <summary>
    /// Creates a new <see cref="EventsList" />.
    /// </summary>
    /// <param name="expectedRevision">
    /// The expected revision of the event stream,
    /// or <see langword="null" /> if the stream is to be created.
    /// </param>
    /// <param name="events">Events to add to the list.</param>
    public EventsList(uint? expectedRevision, IEnumerable<object> events)
        : this(expectedRevision)
    {
        AddRange(events);
    }

    public void Add(object @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _events.Add(@event);
    }

    public void AddRange(IEnumerable<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var e in events)
            Add(e);
    }

    public void Clear() => _events.Clear();

    public bool Contains(object item) => _events.Contains(item);

    public void CopyTo(object[] array, int arrayIndex) => _events.CopyTo(array, arrayIndex);

    public bool Remove(object item) => _events.Remove(item);

    public void Insert(int index, object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _events.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _events.RemoveAt(index);
    }

    public object this[int index]
    {
        get => _events[index];
        set => _events[index] = value ?? throw new ArgumentNullException(nameof(value));
    }

    public int IndexOf(object item) => _events.IndexOf(item);
    
    public int FindIndex(Predicate<object> match) => _events.FindIndex(match);
    
    public int FindLastIndex(Predicate<object> match) => _events.FindLastIndex(match);

    public int Count => _events.Count;

    public bool IsReadOnly => false;

    public IEnumerator<object> GetEnumerator() => _events.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_events).GetEnumerator();

    public override string ToString() =>
        ExpectedRevision.HasValue
            ? $"Count = {Count}. Expected revision: {ExpectedRevision.Value}"
            : $"Count = {Count}";
}