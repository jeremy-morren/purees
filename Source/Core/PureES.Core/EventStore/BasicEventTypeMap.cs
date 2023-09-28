using System.Text;
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
    public Type GetCLRType(string typeName) => 
        Type.GetType(typeName) ??
        throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unable to resolve CLR type");
    
    /// <summary>
    /// Gets a serializable name for a type
    /// </summary>
    /// <param name="type">Type to serialize</param>
    /// <remarks>
    /// Includes assembly information without version (except for <c>System.Private.CoreLib</c> and <c>mscorlib</c>)
    /// </remarks>
    public string GetTypeName(Type type)
    {
        var sb = new StringBuilder(capacity: type.ToString().Length);
        if (type.Namespace != null)
        {
            sb.Append(type.Namespace);
            sb.Append('.');
        }
        sb.Append(type.Name);
        if (type.IsGenericType)
        {
            //Recursively write generic parameters
            sb.Append('[');
            foreach (var t in type.GetGenericArguments())
            {
                var name = GetTypeName(t);
                if (name.IndexOf(',') != -1)
                {
                    //Surrounding braces are only necessary if type contains assembly information
                    sb.Append('[');
                    sb.Append(name);
                    sb.Append("], ");
                }
                else
                {
                    sb.Append(name);
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
                break;
            default:
                sb.Append(", ");
                sb.Append(assembly);
                break;
        }
        
        return sb.ToString();
    }
}