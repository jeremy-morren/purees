using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PureES.SourceGenerators;
using PureES.SourceGenerators.Models;
using PureES.Tests.Framework;
using PureES.Tests.Generators.ReflectedSymbols;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Tests.Generators;

public class DependencyInjectionGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public DependencyInjectionGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateRegisterServices()
    {
        var aggregateTypes = typeof(DependencyInjectionGeneratorTests).Assembly
            .GetExportedTypes()
            .Where(t => t.GetCustomAttribute(typeof(AggregateAttribute)) != null);

        var aggregates = new List<Aggregate>();

        foreach (var t in aggregateTypes)
        {
            var log = new FakeErrorLog();
            var built = AggregateBuilder.BuildAggregate(new ReflectedType(t), out var aggregate, log);
            log.Errors.ShouldBeEmpty();
            built.ShouldBeTrue();
            aggregates.Add(aggregate);
        }

        aggregates.ShouldNotBeEmpty();

        var eventHandlerTypes = typeof(DependencyInjectionGeneratorTests).Assembly
            .GetExportedTypes()
            .Where(t => t.GetCustomAttribute(typeof(EventHandlersAttribute)) != null);
        
        var eventHandlers = new List<EventHandler>();
        foreach (var t in eventHandlerTypes)
        {
            var log = new FakeErrorLog();
            var built = EventHandlersBuilder.BuildEventHandlers(new ReflectedType(t), out var handlers, log);
            log.Errors.ShouldBeEmpty();
            built.ShouldBeTrue();
            eventHandlers.AddRange(handlers);
        }

        eventHandlers.ShouldNotBeEmpty();

        var csharp = DependencyInjectionGenerator.Generate(aggregates, eventHandlers, out var filename);
        
        filename.ShouldNotBeNullOrWhiteSpace();
        csharp.ShouldNotBeNullOrEmpty();
        _output.WriteLine(csharp);
    }
}