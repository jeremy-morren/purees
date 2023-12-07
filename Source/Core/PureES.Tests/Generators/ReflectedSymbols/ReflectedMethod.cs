using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PureES.SourceGenerators.Symbols;

namespace PureES.Tests.Generators.ReflectedSymbols;

internal class ReflectedMethod : ReflectedTokenBase, IMethod
{
    private readonly MethodInfo _method;

    public ReflectedMethod(MethodInfo method) => _method = method;

    public string Name => _method.Name;
    public IType? ReturnType => _method.ReturnType == typeof(void) ? null : new ReflectedType(_method.ReturnType);
    public IType? DeclaringType => _method.DeclaringType != null ? new ReflectedType(_method.DeclaringType) : null;

    public IEnumerable<IParameter> Parameters => _method.GetParameters()
        .Select((p, i) => new ReflectedParameter(i, p));
    
    public IEnumerable<IAttribute> Attributes => _method.GetCustomAttributes(true)
        .Select(a => new ReflectedAttribute(a));

    public bool IsPublic => _method.IsPublic;
    public bool IsStatic => _method.IsStatic;

    public override string ToString() => Name;
    
    #region Equality

    public bool Equals(IMethod? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return other is ReflectedMethod o && _method.Equals(o._method);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is ReflectedMethod o && o._method.Equals(_method);
    }

    public override int GetHashCode()
    {
        return _method.GetHashCode();
    }
    
    #endregion
}