using System.Reflection;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable ReturnTypeCanBeEnumerable.Global

namespace PureES.Core.ExpBuilders.Services;

public class CommandHandlerOptions
{
    private readonly List<Assembly> _assemblies = new();

    public CommandHandlerBuilderOptions BuilderOptions { get; set; } = new();

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies.AsReadOnly();

    public void AddAssembly(Assembly assembly)
    {
        if (!_assemblies.Contains(assembly))
            _assemblies.Add(assembly);
    }
}