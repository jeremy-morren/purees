using System;
using PureES.Core.Tests.Models;
using Xunit;

namespace PureES.Core.Tests.Generators.ReflectedSymbols;

public class ReflectionTests
{
    [Theory]
    [InlineData(typeof(TestAggregates.Aggregate))]
    public void GetReflectedType(Type type)
    {
        var t = new ReflectedType(type);
        t.Constructors.ShouldNotBeEmpty();
        t.Attributes.ShouldNotBeEmpty();
        t.Properties.ShouldNotBeEmpty();
        t.Methods.ShouldNotBeEmpty();
    }
}