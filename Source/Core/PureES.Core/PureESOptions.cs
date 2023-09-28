using PureES.Core.EventStore;

namespace PureES.Core;

[PublicAPI]
public class PureESOptions
{
    /// <summary>
    /// The delegate used to locate the <see langword="string" /> property
    /// that serves as the <c>StreamId</c> for a command or event
    /// </summary>
    /// <remarks>
    /// The delegate will be called recursively until the returned
    /// <see cref="PropertyInfo"/> is a <see langword="string" />
    /// </remarks>
    public Func<Type, PropertyInfo> GetStreamIdProperty { get; set; } = DefaultGetStreamIdProperty;

    /// <summary>
    /// Configure Event handlers
    /// </summary>
    public PureESEventHandlerOptions EventHandlers { get; } = new();

    internal void Validate()
    {
        if (GetStreamIdProperty == null!)
            throw new Exception($"{nameof(GetStreamIdProperty)} is required");
        EventHandlers.Validate();
    }

    private static PropertyInfo DefaultGetStreamIdProperty(Type type)
    {
        const string prop = "StreamId";

        //Efficiency: shouldn't need this
        //if (type == typeof(string)) throw new NotImplementedException();

        return type.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Unable to locate property {prop} on type {type}");
    }
}