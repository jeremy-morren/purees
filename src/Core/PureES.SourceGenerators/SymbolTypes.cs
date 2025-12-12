using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

namespace PureES.SourceGenerators;

internal static class SymbolTypes
{
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