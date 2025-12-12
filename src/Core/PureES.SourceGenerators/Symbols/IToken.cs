using Microsoft.CodeAnalysis;

namespace PureES.SourceGenerators.Symbols;

internal interface IToken
{
    public Location Location { get; }
}