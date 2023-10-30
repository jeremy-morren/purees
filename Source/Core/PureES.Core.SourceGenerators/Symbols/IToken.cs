using Microsoft.CodeAnalysis;

namespace PureES.Core.SourceGenerators.Symbols;

internal interface IToken
{
    public Location Location { get; }
}