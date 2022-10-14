namespace PureES.Core.EventStore.Serialization;

public interface IEventStoreSerializer
{
    /// <summary>
    /// Deserializes persisted data
    /// to <paramref name="objectType"/>
    /// </summary>
    /// <param name="data">Persisted data (can be either Event or Metadata)</param>
    /// <param name="objectType">The destination object type</param>
    /// <returns></returns>
    object? Deserialize(ReadOnlySpan<byte> data, Type? objectType);

    /// <summary>
    /// Serializes a CLR object for persistence
    /// </summary>
    /// <param name="value">The object to serialize</param>
    /// <param name="contentType">The <c>Content-Type</c> of the serialized data</param>
    /// <returns></returns>
    byte[] Serialize(object value, out string contentType);
}