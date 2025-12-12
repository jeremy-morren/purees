namespace PureES.SourceGenerators.Symbols;

internal interface IAssembly : IEquatable<IAssembly>
{
    string Name { get; }
}