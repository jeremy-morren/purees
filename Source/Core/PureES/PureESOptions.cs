namespace PureES;

[PublicAPI]
public class PureESOptions
{
    /// <summary>
    /// Configure Event handlers
    /// </summary>
    public PureESEventHandlerOptions EventHandlers { get; } = new();

    public void Validate()
    {
        EventHandlers.Validate();
    }
}