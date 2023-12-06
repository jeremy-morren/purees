using System;
using System.Linq;
using FluentAssertions;
using PureES.SourceGenerators;
using PureES.Tests.Framework;
using PureES.Tests.Generators.ReflectedSymbols;
using PureES.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Tests.Generators;

public class CommandHandlerGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public CommandHandlerGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(TestAggregate), nameof(TestAggregate.CreateOn))]
    [InlineData(typeof(TestAggregate), nameof(TestAggregate.CreateOnAsyncEnumerable))]
    [InlineData(typeof(TestAggregate), nameof(TestAggregate.UpdateOn))]
    [InlineData(typeof(TestAggregate), nameof(TestAggregate.UpdateOnResult))]
    [InlineData(typeof(ImplementedGenericAggregate), nameof(ImplementedGenericAggregate.Create))]
    public void GenerateCSharp(Type aggregateType, string methodName)
    {
        var log = new FakeErrorLog();

        var built = AggregateBuilder.BuildAggregate(new ReflectedType(aggregateType, false), out var aggregate, log);

        log.Errors.ShouldBeEmpty();
        built.ShouldBeTrue();

        aggregate.Handlers.Should().ContainSingle(h => h.Method.Name == methodName);

        var handler = aggregate.Handlers.Single(h => h.Method.Name == methodName);

        var result = CommandHandlerGenerator.Generate(aggregate, handler, out var filename);
        filename.ShouldNotBeNullOrWhiteSpace();
        result.ShouldNotBeNullOrEmpty();
        _output.WriteLine(result);
    }
}