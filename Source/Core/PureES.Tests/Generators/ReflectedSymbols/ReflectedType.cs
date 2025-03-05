using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PureES.SourceGenerators.Symbols;

namespace PureES.Tests.Generators.ReflectedSymbols;

internal class ReflectedType : ReflectedTokenBase, IType
{
    private readonly Type _type;
    private readonly bool? _isPartial;
    private readonly bool? _isNullable;

    public ReflectedType(Type type, 
        bool? isPartial = null,
        bool? isNullable = null)
    {
        _type = type ?? throw new ArgumentNullException(nameof(type));
        
        _isPartial = isPartial;
        _isNullable = isNullable;
    }

    public IAssembly Assembly => new ReflectedAssembly(_type.Assembly);
    public IType? BaseType => _type.BaseType != null ? new ReflectedType(_type.BaseType) : null;

    public IType? ContainingType => _type.DeclaringType != null ? new ReflectedType(_type.DeclaringType) : null;

    public string? Namespace => _type.Namespace;
    
    public string Name => _type.Name;

    public string FullName => $"{_type.Namespace}.{_type.Name}";

    public bool IsInterface => _type.IsInterface;
    
    public bool IsReferenceType => !_type.IsValueType;
    
    public bool IsAbstract => _type.IsAbstract;

    public bool IsGenericType => _type.IsGenericType;

    public IEnumerable<IAttribute> Attributes => _type.GetCustomAttributes(false)
        .Select(a => new ReflectedAttribute(a));

    public IEnumerable<IConstructor> Constructors => _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .Select(c => new ReflectedConstructor(_type, c));

    /// <summary>
    /// Reflects including static and non-static
    /// </summary>
    private IEnumerable<T> Reflect<T>(Func<Type, BindingFlags, IEnumerable<T>> get) where T : MemberInfo
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        return get(_type, flags).Where(i => i.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null);
    }
    
    public IEnumerable<IProperty> Properties => Reflect((t,f) => t.GetProperties(f))
        .Select(p => new ReflectedProperty(p));

    public IEnumerable<IMethod> Methods => Reflect((t, f) => t.GetMethods(f))
        .Select(m => new ReflectedMethod(m));

    public IEnumerable<IType> GenericArguments => _type.GetGenericArguments().Select(t => new ReflectedType(t));
    public IEnumerable<IType> ImplementedInterfaces => _type.GetInterfaces().Select(i => new ReflectedType(i));

    public override string ToString() => CSharpName;

    public string GetFullName(bool nullable) => FullName;

    public string CSharpName
    {
        get
        {
            if (!_type.IsGenericType || Nullable.GetUnderlyingType(_type) != null || !_type.Name.Contains('`'))
                return LangKeyword(_type);
            var n = _type.Name[.._type.Name.IndexOf('`')]; //Get name without `
            var sb = new StringBuilder($"global::{_type.Namespace}.{n}");
            sb.Append('<');
            foreach (var param in _type.GetGenericArguments())
                sb.Append($"{new ReflectedType(param).CSharpName},");
            sb[^1] = '>'; //Replace trailing ',' with '>'
            return sb.ToString();
        }
    }
    
    #region Equality

    public bool Equals(IType? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not ReflectedType t) return false;
        return t._type == _type;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not ReflectedType other) return false;
        return other._type == _type;
    }

    public override int GetHashCode() => ToString().GetHashCode();
    
    #endregion

    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    private static string LangKeyword(Type type)
    {
        if (type == typeof(object))
            return "object";
        
        var nullable = Nullable.GetUnderlyingType(type) != null ? "?" : string.Empty;
        var nt = Nullable.GetUnderlyingType(type) ?? type;
        
        if (nt == typeof(int)) return $"int{nullable}";
        if (nt == typeof(string)) return $"string{nullable}";
        if (nt == typeof(double)) return $"double{nullable}";
        if (nt == typeof(bool)) return $"bool{nullable}";
        
        return $"global::{type.FullName}";
    }
}