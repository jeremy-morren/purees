// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable ReturnTypeCanBeEnumerable.Global

namespace PureES.Core;

public class PureESOptions
{
    private readonly List<Assembly> _assemblies = new();

    public PureESBuilderOptions BuilderOptions { get; set; } = new();

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies.AsReadOnly();

    public void AddAssembly(Assembly assembly)
    {
        if (!_assemblies.Contains(assembly))
            _assemblies.Add(assembly);
    }
}