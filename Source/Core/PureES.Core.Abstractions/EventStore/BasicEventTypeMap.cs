using System.Reflection;

namespace PureES.Core.EventStore;

/// <summary>
///     A basic implementation o f <see cref="IEventTypeMap" />
///     which maps via <see cref="Type.Name" />
/// </summary>
public class BasicEventTypeMap : IEventTypeMap
{
    private readonly Dictionary<string, Type> _types = new();
    
    public Type GetCLRType(string typeName) => _types.TryGetValue(typeName, out var type)
        ? type
        : throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unable to resolve CLR type");

    public string GetTypeName(Type type)
    {
        //Get type name without namespace
        var name = type.Namespace == null
            ? type.FullName
            : type.FullName?[(type.Namespace.Length + 1)..];
        return name ?? throw new InvalidOperationException($"Unable to get name for type {type}");
    }

    /// <summary>
    ///     Adds all public types in an assembly to the map
    /// </summary>
    /// <param name="assembly">Assembly to add types for</param>
    public void AddAssembly(Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => t is {IsAbstract: false, IsInterface: false});
        foreach (var t in types)
            AddType(t);
    }


    /// <summary>
    /// Configure whether or not <see cref="AddType"/> throws
    /// an exception if 2 types with duplicate names are found
    /// (default <see langword="false" />
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the first type is registered
    /// </remarks>
    public bool ThrowOnDuplicateTypeName { get; set; } = false;

    /// <summary>
    ///     Adds a type to the map
    /// </summary>
    /// <param name="type">Type to add</param>
    /// <exception cref="InvalidOperationException">
    ///     Type with identical name already added
    /// </exception>
    public void AddType(Type type)
    {
        var str = GetTypeName(type);
        if (_types.TryAdd(str, type)) return;
        if (ThrowOnDuplicateTypeName)
            throw new InvalidOperationException($"Duplicate type '{str}'");
    }
}