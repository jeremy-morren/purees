﻿using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;

namespace PureES;

/// <summary>
///     A basic implementation o f <see cref="IEventTypeMap" />
///     which maps via <see cref="Type.Name" />
/// </summary>
[PublicAPI]
public class BasicEventTypeMap : IEventTypeMap
{
    ImmutableArray<string> IEventTypeMap.GetTypeNames(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return NamesMap.GetOrAdd(eventType, GetTypeNames);
    }

    Type IEventTypeMap.GetCLRType(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        return TypesMap.GetOrAdd(typeName, GetCLRType);
    }

    // Used by AggregateFactory for showing a type name in exceptions
    public static string GetTypeName(Type type) => GetTypeNames(type)[^1];

    private static readonly ConcurrentDictionary<string, Type> TypesMap = new();
    private static readonly ConcurrentDictionary<Type, ImmutableArray<string>> NamesMap = new();
    
    public static Type GetCLRType(string typeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);

        return Type.GetType(typeName) 
               ?? throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unable to resolve CLR type");
    }

    /// <summary>
    /// Gets type names (full inheritance hierarchy) for a type
    /// </summary>
    public static ImmutableArray<string> GetTypeNames(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var result = new List<string>();
        while (true)
        {
            GetTypeName(eventType, out var name, out _);
            result.Add(name);
            if (eventType.BaseType == typeof(object) || eventType.BaseType == null)
                break;
            eventType = eventType.BaseType;
        }
        result.Reverse(); //Base type first
        return [..result];
    }
    
    /// <summary>
    /// Gets a serializable name for a type
    /// </summary>
    /// <remarks>
    /// Includes assembly information without version (except for <c>System.Private.CoreLib</c> and <c>mscorlib</c>)
    /// </remarks>
    public static void GetTypeName(Type type, out string name, out bool includesAssembly)
    {
        ArgumentNullException.ThrowIfNull(type);

        var sb = new StringBuilder(capacity: type.ToString().Length);
        if (type.FullName == null)
            throw new ArgumentOutOfRangeException(nameof(type), type, "Cannot serialize a partially defined type");

        if (type.Namespace != null)
        {
            sb.Append(type.Namespace);
            sb.Append('.');
        }

        sb.Append(GetNestedName(type));
        if (type.IsGenericType)
        {
            //Recursively write generic parameters
            sb.Append('[');
            foreach (var t in type.GetGenericArguments())
            {
                GetTypeName(t, out var genericName, out var hasAssembly);
                
                //Surrounding braces are only necessary if type contains assembly information
                if (hasAssembly)
                {
                    sb.Append('[');
                    sb.Append(genericName);
                    sb.Append("], ");
                }
                else
                {
                    sb.Append(genericName);
                    sb.Append(", ");
                }
            }
            sb.Length -= 2; //Trim trailing ', '
            sb.Append(']');
        }
        
        var assembly = type.Assembly.GetName().Name;

        switch (assembly)
        {
            case "System.Private.CoreLib":
            case "mscorlib":
            case null:
                includesAssembly = false;
                break;
            default:
                includesAssembly = true;
                sb.Append(", ");
                sb.Append(assembly);
                break;
        }
        
        name = sb.ToString();
    }

    private static string GetNestedName(Type type) => type.IsNested ? $"{GetNestedName(type.DeclaringType!)}+{type.Name}" : type.Name;
}