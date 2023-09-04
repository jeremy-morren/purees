using System;
using System.Linq;
using PureES.Core.Generators;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
using Xunit.Abstractions;

namespace PureES.Core.Tests.Generators;

public class CommandHandlerGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public CommandHandlerGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(TestAggregates.Aggregate), nameof(TestAggregates.Aggregate.CreateOn))]
    [InlineData(typeof(TestAggregates.Aggregate), nameof(TestAggregates.Aggregate.CreateOnAsyncEnumerable))]
    [InlineData(typeof(TestAggregates.Aggregate), nameof(TestAggregates.Aggregate.UpdateOn))]
    [InlineData(typeof(TestAggregates.Aggregate), nameof(TestAggregates.Aggregate.UpdateOnResult))]
    public void GenerateCSharp(Type aggregateType, string methodName)
    {
        var log = new FakeErrorLog();

        var built = AggregateBuilder.Build(new ReflectedType(aggregateType, false), out var aggregate, log);

        log.Errors.ShouldBeEmpty();
        built.ShouldBeTrue();

        aggregate.Handlers.Should().ContainSingle(h => h.Method.Name == methodName);

        var handler = aggregate.Handlers.Single(h => h.Method.Name == methodName);

        var result = CommandHandlerGenerator.Generate(aggregate, handler);
        result.ShouldNotBeNullOrEmpty();
        _output.WriteLine(result);
    }
}