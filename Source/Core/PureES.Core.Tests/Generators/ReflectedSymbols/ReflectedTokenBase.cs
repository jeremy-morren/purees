using Microsoft.CodeAnalysis;

namespace PureES.Core.Tests.Generators.ReflectedSymbols;

internal abstract class ReflectedTokenBase
{
    public Location Location { get; } = Location.None;
}