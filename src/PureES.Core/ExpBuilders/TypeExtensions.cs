using System.Reflection;
using System.Runtime.CompilerServices;

namespace PureES.Core.ExpBuilders;

internal static class TypeExtensions
{
    private static readonly NullabilityInfoContext Nullability = new();
    public static bool IsNullable(this Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    [MethodImpl(MethodImplOptions.Synchronized)] //This is necessary for some reason
    public static bool IsNullable(this ParameterInfo parameter)
    {
        if (IsNullable(parameter.ParameterType)) return true;
        return Nullability.Create(parameter).WriteState == NullabilityState.Nullable;
    }

    public static bool IsStruct(this Type type)
    {
        return type.IsValueType && !type.IsPrimitive;
    }
    
    public static bool IsTask(this Type type, out Type? valueType)
    {
        switch (type.GetGenericArguments().Length)
        {
            case 0:
                valueType = null;
                if (type == typeof(ValueTask))
                    throw new InvalidOperationException("ValueTask return types are not supported");
                return type == typeof(Task);
            case 1:
            {
                var t = type.GetGenericArguments()[0];
                valueType = t;
                if (typeof(ValueTask<>).MakeGenericType(t) == type)
                    throw new InvalidOperationException("ValueTask return types are not supported");
                return typeof(Task<>).MakeGenericType(t) == type;
            }
            default:
                valueType = null;
                return false;
        }
    }
}