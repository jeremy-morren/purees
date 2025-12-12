using System.Reflection;
using PureES.SourceGenerators.Symbols;

namespace PureES.Tests.Generators.ReflectedSymbols;

internal class ReflectedAssembly : IAssembly
{
    private readonly Assembly _assembly;

    public ReflectedAssembly(Assembly assembly) => _assembly = assembly;

    public string Name => _assembly.GetName().Name!;

    public string FullName => _assembly.FullName!;

    public override string ToString() => FullName;
    
    #region Equality

    public bool Equals(IAssembly? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not ReflectedAssembly o) return false;
        return o._assembly == _assembly;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not ReflectedAssembly o) return false;
        return o._assembly == _assembly;
    }

    public override int GetHashCode() => FullName.GetHashCode();

    #endregion
}