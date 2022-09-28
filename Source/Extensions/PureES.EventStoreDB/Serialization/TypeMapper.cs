using System.Reflection;

namespace PureES.EventStoreDB.Serialization;

public class TypeMapper
{
    private readonly Dictionary<string, Type> _types = new();

    public void AddAssembly(Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface);
        foreach (var t in types)
            AddType(t);
    }

    public void AddType(Type type)
    {
        var str = GetString(type);
        if (_types.TryAdd(str, type)) return;
        throw new InvalidOperationException($"Duplicate type '{str}'");
    }

    public Type GetType(string type)
    {
        if (_types.TryGetValue(type, out var resolved))
            return resolved;
        throw new ArgumentException($"Unknown event type {type}");
    }

    public static string GetString(Type type)
    {
        //Get type name without namespace
        var name = type.Namespace == null
            ? type.FullName
            : type.FullName?[(type.Namespace.Length + 1)..];
        return name ?? throw new InvalidOperationException($"Unable to get name for type {type}");
    }
}