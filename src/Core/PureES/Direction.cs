namespace PureES;

/// <summary>
/// The direction that an event store stream should be read
/// </summary>
[PublicAPI]
public enum Direction
{
    /// <summary>
    /// The stream should be read in a forwards direction
    /// </summary>
    Forwards = 1,
    
    /// <summary>
    /// The stream should be read in a backwards direction
    /// </summary>
    Backwards = 2
}