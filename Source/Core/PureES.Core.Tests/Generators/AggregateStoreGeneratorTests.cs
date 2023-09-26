using System;
using FluentAssertions;
using PureES.Core.Generators;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Core.Tests.Generators;

public class AggregateStoreGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public AggregateStoreGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(TestAggregates.Aggregate), 6)]
    public void Generate(Type aggregateType, int whenCount)
    {
        var log = new FakeErrorLog();

        var built = PureESTreeBuilder.BuildAggregate(new ReflectedType(aggregateType, false), out var aggregate, log);

        log.Errors.ShouldBeEmpty();
        built.ShouldBeTrue();

        aggregate.When.ShouldNotBeEmpty();
        aggregate.When.Should().HaveCount(whenCount);

        var result = AggregateStoreGenerator.Generate(aggregate, out var filename);
        filename.ShouldNotBeNullOrWhiteSpace();
        result.ShouldNotBeNullOrEmpty();
        _output.WriteLine(result);
    }
}