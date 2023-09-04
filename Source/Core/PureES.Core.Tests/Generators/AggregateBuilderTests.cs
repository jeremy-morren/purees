using System;
using PureES.Core.Generators;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;

namespace PureES.Core.Tests.Generators;

public class AggregateBuilderTests
{
    [Theory]
    [InlineData(typeof(TestAggregates.Aggregate))]
    public void BuildAggregate(Type aggregate)
    {
        var log = new FakeErrorLog();

        var success = AggregateBuilder.Build(new ReflectedType(aggregate, true), out var tree, log);

        log.Errors.ShouldBeEmpty();
        success.ShouldBeTrue();

        tree.ShouldNotBeNull();
        tree.Type.ShouldNotBeNull();
        tree.Handlers.ShouldNotBeEmpty();
        tree.When.ShouldNotBeEmpty();
    }
}