﻿using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;

namespace PureES.Core.Generators.Aggregates;

internal static class TypeHelpers
{
    public static int IndexOf(this IReadOnlyList<IType> list, IType item)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i].Equals(item))
                return i;
        return -1;
    }
    
    [Pure]
    public static bool IsNonGenericEventEnvelope(this IType type)
    {
        return typeof(EventEnvelope).FullName!.Equals(type.FullName, StringComparison.Ordinal);
    }

    [Pure]
    public static bool IsGenericEventEnvelope(this IType type, out IType eventType)
    {
        eventType = null!;
        if (!type.IsGenericType || !type.IsEventEnvelope())
            return false;
        
        //We assume that the event type is GenericParameters[0]
        //This would only be a problem if the user had some very weird
        //inheritance going on

        eventType = type.GenericArguments.First();
        return true;
    }

    /// <summary>
    /// Checks that a type in the inheritance hierarchy is NonGenericEventEnvelope
    /// </summary>
    [Pure]
    public static bool IsEventEnvelope(this IType type)
    {
        return type.IsNonGenericEventEnvelope() || 
               (type.BaseType != null && IsEventEnvelope(type.BaseType));
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
    public static bool IsEnumerable(this IType type)
    {
        return (type.IsInterface && type.IsGenericType(typeof(IEnumerable<>))) ||
               //Check implemented interfaces
               type.ImplementedInterfaces.Any(t => t.IsGenericType(typeof(IEnumerable<>)));
    }
    
    [Pure]
    public static bool IsAsyncEnumerable(this IType type)
    {
        return (type.IsInterface && type.IsGenericType(typeof(IAsyncEnumerable<>)))
               //Check implemented interfaces
               || type.ImplementedInterfaces.Any(t => t.IsGenericType(typeof(IAsyncEnumerable<>)));
    }
    
    [Pure]
    public static bool IsCancellationToken(this IType type)
    {
        return type.FullName.Equals(typeof(CancellationToken).FullName, StringComparison.Ordinal);
    }

    private static bool IsGenericType(this IType type, Type other)
    {
        var name = other.FullName!;
        //Remove bit after `
        var index = name.IndexOf('`');
        return type.FullName.StartsWith(name.Substring(0, index), StringComparison.Ordinal);
    }
}