using IType = PureES.SourceGenerators.Symbols.IType;
using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

namespace PureES.SourceGenerators;

internal static class PureESSymbolTypes
{
    [Pure]
    public static bool IsNonGenericEventEnvelope(this IType type) => 
        type is { IsGenericType: false, FullName: PureESSymbols.EventEnvelope };

    /// <summary>
    /// Determines if type is EventEnvelope or subclass
    /// </summary>
    [Pure]
    public static bool IsGenericEventEnvelope(this IType type, out IType eventType, out IType metadataType)
    {
        if (type.IsGenericType && type.FullName.StartsWith(PureESSymbols.EventEnvelope, StringComparison.Ordinal))
        {
            var args = type.GenericArguments.ToList();
            eventType = args[0];
            metadataType = args[1];
            return true;
        }
        eventType = null!;
        metadataType = null!;
        return type.BaseType != null && IsGenericEventEnvelope(type.BaseType, out eventType, out metadataType);
    }

    [Pure]
    public static bool IsGenericEventEnvelope(this IType type) => type.IsGenericEventEnvelope(out _, out _);

    /// <summary>
    /// Checks that a type in the inheritance hierarchy is event envelope
    /// </summary>
    [Pure]
    public static bool IsEventEnvelope(this IType type)
    {
        return type.IsNonGenericEventEnvelope()
               || type.IsGenericEventEnvelope(out _, out _)
               || (type.BaseType != null && IsEventEnvelope(type.BaseType));
    }

    /// <summary>
    /// Checks that a type is CommandResult or subclass
    /// </summary>
    [Pure]
    public static bool IsCommandResultType(this IType type, out IType eventType, out IType resultType)
    {
        if (type.IsGenericType && type.FullName.StartsWith(PureESSymbols.CommandResult, StringComparison.Ordinal))
        {
            var args = type.GenericArguments.ToList();
            eventType = args[0];
            resultType = args[1];
            return true;
        }

        eventType = null!;
        resultType = null!;
        return type.BaseType != null && IsCommandResultType(type.BaseType, out eventType, out resultType);
    }

    [Pure]
    public static bool IsEventsTransaction(this IType type)
    {
        return type.GetFullName(false) == PureESSymbols.EventsTransaction ||
               type.ImplementedInterfaces.Any(i => i.GetFullName(false) == PureESSymbols.EventsTransaction);
    }

    public static bool HasCommandAttribute(this IParameter parameter) => 
        parameter.Attributes.Contains(PureESSymbols.CommandAttribute);
    public static bool HasEventAttribute(this IParameter parameter) => 
        parameter.Attributes.Contains(PureESSymbols.EventAttribute);

    public static bool IsStreamId(this IAttribute attribute) => 
        attribute.Type.FullName == PureESSymbols.StreamIdAttribute;

    public static bool HasAggregateAttribute(this IType type) =>
        type.Attributes.Contains(PureESSymbols.AggregateAttribute);
    
    public static bool HasEventHandlersAttribute(this IType type) =>
        type.Attributes.Contains(PureESSymbols.EventHandlersAttribute);
}