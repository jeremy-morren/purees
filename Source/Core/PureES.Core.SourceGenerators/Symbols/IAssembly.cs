namespace PureES.Core.SourceGenerators.Symbols;

internal interface IAssembly : IEquatable<IAssembly>
{
    string Name { get; }
}