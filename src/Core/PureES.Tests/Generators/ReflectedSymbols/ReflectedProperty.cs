using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PureES.SourceGenerators.Symbols;

namespace PureES.Tests.Generators.ReflectedSymbols;

internal class ReflectedProperty : ReflectedTokenBase, IProperty
{
    private readonly PropertyInfo _source;

    public ReflectedProperty(PropertyInfo source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public string Name => _source.Name;

    public IType Type => new ReflectedType(_source.PropertyType, isNullable: IsNullable);

    public IEnumerable<IAttribute> Attributes => _source.GetCustomAttributes()
        .Select(a => new ReflectedAttribute(a));
    
    public bool IsPublic => _source.GetGetMethod(false) != null;

    public bool IsStatic => _source.GetGetMethod(true) is { IsStatic : true };

    public bool CanRead => _source.CanRead;

    public bool CanWrite => _source.CanWrite;

    public bool IsInitOnly
    {
        get
        {
            if (!_source.CanWrite || _source.SetMethod == null)
                return false;
            
            var setMethod = _source.SetMethod;
 
            // Get the modifiers applied to the return parameter.
            var setMethodReturnParameterModifiers = setMethod.ReturnParameter.GetRequiredCustomModifiers();
 
            // Init-only properties are marked with the IsExternalInit type.
            return setMethodReturnParameterModifiers.Any(t => t.Name == "IsExternalInit");
        }
    }

    public bool IsNullable => _source.IsNullable();

    public bool IsRequired =>
        _source.GetCustomAttributes().Any(a => a.GetType().Name == "RequiredMemberAttribute");

    public string? DocumentationXml => null;

    public override string ToString() => Name;

    #region Equality

    public bool Equals(IProperty? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not ReflectedProperty o) return false;
        return _source.Equals(o._source);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not ReflectedProperty o) return false;
        return _source.Equals(o._source);
    }

    public override int GetHashCode() => _source.GetHashCode();

    #endregion
}