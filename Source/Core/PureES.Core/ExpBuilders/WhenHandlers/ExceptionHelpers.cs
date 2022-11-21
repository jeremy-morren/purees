using System.Reflection;

namespace PureES.Core.ExpBuilders.WhenHandlers;

internal class ExceptionHelpers
{
    public static readonly MethodInfo ThrowCreatedWhenBaseMethod =
        typeof(ExceptionHelpers).GetMethod(nameof(ThrowCreatedWhenBase), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException($"Unable to get {nameof(ThrowCreatedWhenBase)} method");

    public static readonly MethodInfo ThrowUpdatedWhenBaseMethod =
        typeof(ExceptionHelpers).GetMethod(nameof(ThrowUpdatedWhenBase), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException($"Unable to get {nameof(ThrowUpdatedWhenBase)} method");

    public static void ThrowCreatedWhenBase(Type aggregateType, object @event)
    {
        throw new ArgumentException(
            $"No suitable CreateWhen method found on {aggregateType} for event {@event.GetType()}");
    }

    public static void ThrowUpdatedWhenBase(Type aggregateType, object @event)
    {
        throw new ArgumentException(
            $"No suitable UpdateWhen method found on {aggregateType} for event {@event.GetType()}");
    }
}