using Microsoft.Extensions.Options;

namespace PureES.Core.EventStore;

/// <summary>
///     A basic implementation o f <see cref="IEventTypeMap" />
///     which maps via <see cref="Type.Name" />
/// </summary>
[PublicAPI]
public class BasicEventTypeMap : IEventTypeMap
{
    private readonly Dictionary<string, Type> _types = new();
    
    public BasicEventTypeMap(IOptions<PureESOptions> options)
    {
        var types = options.Value.Assemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t is {IsAbstract: false, IsInterface: false});
        foreach (var t in types)
        {
            var key = GetTypeName(t);
            if (!_types.ContainsKey(key))
                _types.Add(key, t);
        }
    }
    
    public Type GetCLRType(string typeName) => _types.TryGetValue(typeName, out var type)
        ? type
        : throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unable to resolve CLR type");

    public string GetTypeName(Type type)
    {
        //Get type name without namespace
        var name = type.Namespace == null
            ? type.FullName
            : type.FullName?.Substring(type.Namespace.Length + 1);
        return name ?? throw new InvalidOperationException($"Unable to get name for type {type}");
    }
}