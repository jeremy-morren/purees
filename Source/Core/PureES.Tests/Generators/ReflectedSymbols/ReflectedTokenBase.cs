using Microsoft.CodeAnalysis;

namespace PureES.Tests.Generators.ReflectedSymbols;

internal abstract class ReflectedTokenBase
{
    public Location Location { get; } = Location.None;
}