namespace PureES.Core.Generators.Framework;

internal static class SymbolHelpers
{
    public static IEnumerable<IMethod> GetMethodsRecursive(this IType type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var methods = type.Methods;

        //Return static methods from base type
        var t = type;
        while ((t = t.BaseType) != null)
            methods = methods.Concat(t.Methods.Where(m => m.IsStatic));
        
        return methods;
    }
}