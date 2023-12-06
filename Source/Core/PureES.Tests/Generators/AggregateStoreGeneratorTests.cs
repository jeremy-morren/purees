using System;
using FluentAssertions;
using PureES.SourceGenerators;
using PureES.Tests.Framework;
using PureES.Tests.Generators.ReflectedSymbols;
using PureES.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Tests.Generators;

public class AggregateStoreGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public AggregateStoreGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(TestAggregate), 6)]
    public void Generate(Type aggregateType, int whenCount)
    {
        var log = new FakeErrorLog();

        var built = AggregateBuilder.BuildAggregate(new ReflectedType(aggregateType, false), out var aggregate, log);

        log.Errors.ShouldBeEmpty();
        built.ShouldBeTrue();

        aggregate.When.ShouldNotBeEmpty();
        aggregate.When.Should().HaveCount(whenCount);

        var result = AggregateFactoryGenerator.Generate(aggregate, out var filename);
        filename.ShouldNotBeNullOrWhiteSpace();
        result.ShouldNotBeNullOrEmpty();
        _output.WriteLine(result);
    }
}