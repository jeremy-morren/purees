using Microsoft.CodeAnalysis;

namespace PureES.Core.Generators.Symbols;

internal interface IToken
{
    public Location Location { get; }
}