﻿using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace PureES.Core.EventStore;

/// <summary>
///     A basic implementation o f <see cref="IEventTypeMap" />
///     which maps via <see cref="Type.Name" />
/// </summary>
[PublicAPI]
public class BasicEventTypeMap : IEventTypeMap
{
    public Type GetCLRType(string typeName)
    {
        if (typeName == null) throw new ArgumentNullException(nameof(typeName));
        
        return Type.GetType(typeName) 
               ?? throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unable to resolve CLR type");
    }

    public string GetTypeName(Type eventType)
    {
        if (eventType == null) throw new ArgumentNullException(nameof(eventType));
        
        GetTypeName(eventType, out var name, out _);
        return name;
    }
    
    /// <summary>
    /// Gets a serializable name for a type
    /// </summary>
    /// <remarks>
    /// Includes assembly information without version (except for <c>System.Private.CoreLib</c> and <c>mscorlib</c>)
    /// </remarks>
    public static void GetTypeName(Type type, out string name, out bool includesAssembly)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        
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