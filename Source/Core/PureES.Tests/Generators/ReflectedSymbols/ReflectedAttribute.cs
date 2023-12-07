using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using PureES.SourceGenerators.Symbols;

namespace PureES.Tests.Generators.ReflectedSymbols;

internal class ReflectedAttribute : ReflectedTokenBase, IAttribute
{
    private readonly Attribute _value;

    public ReflectedAttribute(object value)
    {
        if (value is not Attribute a)
            throw new ArgumentException("Value is not an Attribute", nameof(value));
        _value = a;
    }

    public IType Type => new ReflectedType(_value.GetType());

    public IType TypeParameter => GetProperty(t => new ReflectedType((Type)t));
    
    public string StringParameter => GetProperty(o => (string)o);

    public string[] StringParams => GetProperty(o => (string[])o);

    private T GetProperty<T>(Func<object, T> construct)
    {
        try
        {

            //We just return the first property
            var prop = _value.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(a => a.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null);
            var value = prop?.GetValue(_value);
            return value == null ? default! : construct(value);
        }
        catch (InvalidCastException)
        {
            return default!;
        }
    }

    public override string? ToString() => Type.ToString();
}