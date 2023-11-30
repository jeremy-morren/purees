using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

namespace PureES.Core.SourceGenerators.Generators;

internal static class SymbolTypes
{
    [Pure]
    public static bool IsNonGenericEventEnvelope(this IType type)
    {
        return typeof(EventEnvelope).FullName!.Equals(type.FullName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if type is <see cref="EventEnvelope{TEvent,TMetadata}"/> or subclass
    /// </summary>
    [Pure]
    public static bool IsGenericEventEnvelope(this IType type, out IType eventType, out IType metadataType)
    {
        if (type.IsGenericType && type.IsGenericType(typeof(EventEnvelope<,>)))
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

    /// <summary>
    /// Determines if type is <see cref="EventEnvelope{TEvent,TMetadata}"/> or subclass
    /// </summary>
    [Pure]
    public static bool IsGenericEventEnvelope(this IType type) => type.IsGenericEventEnvelope(out _, out _);

    /// <summary>
    /// Checks that a type in the inheritance hierarchy is NonGenericEventEnvelope
    /// </summary>
    [Pure]
    public static bool IsEventEnvelope(this IType type)
    {
        return type.IsNonGenericEventEnvelope()
               || type.IsGenericEventEnvelope(out _, out _)
               || (type.BaseType != null && IsEventEnvelope(type.BaseType));
    }

    /// <summary>
    /// Checks that a type is <see cref="CommandResult{TEvent, TResult}"/> or subclass
    /// </summary>
    [Pure]
    public static bool IsCommandResultType(this IType type, out IType eventType, out IType resultType)
    {
        if (type.IsGenericType && IsGenericType(type, typeof(CommandResult<,>)))
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
    public static bool IsAsync(this IType type, out IType? underlyingType)
    {
        if (!type.IsGenericType)
        {
            underlyingType = null;
            return type.FullName == typeof(Task).FullName || type.FullName == typeof(ValueTask).FullName;
        }
        underlyingType = type.GenericArguments.First();
        
        //Check if type name STARTS with (ignoring generic part)
        return type.FullName.StartsWith(typeof(ValueTask).FullName!, StringComparison.Ordinal)
               || type.FullName.StartsWith(typeof(Task).FullName!, StringComparison.Ordinal);
    }

    [Pure]
    public static bool IsEventsTransaction(this IType type) => type.FullName == typeof(EventsTransaction).FullName;

    [Pure]
    public static bool IsEnumerable(this IType type)
    {
        return (type.IsInterface && type.IsGenericType(typeof(IEnumerable<>))) ||
               //Check implemented interfaces
               type.ImplementedInterfaces.Any(t => t.IsGenericType(typeof(IEnumerable<>)));
    }
    
    [Pure]
    public static bool IsAsyncEnumerable(this IType type)
    {
        return (type.IsInterface && type.FullName.StartsWith(ExternalTypes.IAsyncEnumerableBase, StringComparison.Ordinal))
               //Check implemented interfaces
               || type.ImplementedInterfaces.Any(IsAsyncEnumerable);
    }
    
    [Pure]
    public static bool IsCancellationToken(this IType type)
    {
        return type.FullName.Equals(typeof(CancellationToken).FullName, StringComparison.Ordinal);
    }
    
    private static bool IsGenericType(this IType type, Type other)
    {
        if (!other.IsGenericType) throw new NotImplementedException();
        if (!type.IsGenericType) return false;
        
        if (type.GenericArguments.Count() != other.GetGenericArguments().Length)
            return false;
        
        var name = other.FullName!;
        //Remove anything after `
        var index = name.IndexOf('`');
        return type.FullName.StartsWith(name.Substring(0, index), StringComparison.Ordinal);
    }
}