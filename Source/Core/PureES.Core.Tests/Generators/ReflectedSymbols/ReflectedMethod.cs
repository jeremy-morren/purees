using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PureES.Core.Generators.Symbols;

namespace PureES.Core.Tests.Generators.ReflectedSymbols;

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
}