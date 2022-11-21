using System.Reflection;
using System.Runtime.CompilerServices;

namespace PureES.Core.ExpBuilders;

internal static class TypeExtensions
{
    private static readonly NullabilityInfoContext Nullability = new();
    public static bool IsNullable(this Type type) => Nullable.GetUnderlyingType(type) != null;

    [MethodImpl(MethodImplOptions.Synchronized)] //This is necessary for some reason
    public static bool IsNullable(this ParameterInfo parameter)
    {
        if (IsNullable(parameter.ParameterType)) return true;
        return Nullability.Create(parameter).WriteState == NullabilityState.Nullable;
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //This is necessary for some reason
    public static bool IsNullable(this PropertyInfo property)
    {
        if (IsNullable(property.PropertyType)) return true;
        return Nullability.Create(property).WriteState == NullabilityState.Nullable;
    }

    public static bool IsStruct(this Type type) => type.IsValueType && !type.IsPrimitive;

    public static bool IsTask(this Type type, out Type? valueType)
    {
        switch (type.GetGenericArguments().Length)
        {
            case 0:
                valueType = null;
                return type == typeof(Task);
            case 1:
            {
                var t = type.GetGenericArguments()[0];
                valueType = t;
                return typeof(Task<>).MakeGenericType(t) == type;
            }
            default:
                valueType = null;
                return false;
        }
    }

    public static bool IsValueTask(this Type type, out Type? valueType)
    {
        switch (type.GetGenericArguments().Length)
        {
            case 0:
                valueType = null;
                return type == typeof(ValueTask);
            case 1:
            {
                var t = type.GetGenericArguments()[0];
                valueType = t;
                return typeof(ValueTask<>).MakeGenericType(t) == type;
            }
            default:
                valueType = null;
                return false;
        }
    }

    public static bool IsAsyncEnumerable(this Type type)
    {
        if (type.BaseType != null && type.BaseType != typeof(object))
            return IsAsyncEnumerable(type.BaseType);
        return type.GetGenericArguments().Length == 1 &&
               typeof(IAsyncEnumerable<>).MakeGenericType(type.GetGenericArguments()[0]) == type;
    }

    public static MethodInfo GetStaticMethod(this Type type, string name)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                   .SingleOrDefault(m => m.Name == name)
               ?? throw new InvalidOperationException($"Unable to get method {type}+{name}");
    }
}