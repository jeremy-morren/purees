using System;
using System.Linq;
using PureES.Core.Generators;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
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
    [InlineData(typeof(TestAggregates.Aggregate), 5)]
    public void Generate(Type aggregateType, int whenCount)
    {
        var log = new FakeErrorLog();

        var built = AggregateBuilder.Build(new ReflectedType(aggregateType, false), out var aggregate, log);

        log.Errors.ShouldBeEmpty();
        built.ShouldBeTrue();

        aggregate.When.ShouldNotBeEmpty();
        aggregate.When.Should().HaveCount(whenCount);

        var result = AggregateStoreGenerator.Generate(aggregate);
        result.ShouldNotBeNullOrEmpty();
        _output.WriteLine(result);
    }
}