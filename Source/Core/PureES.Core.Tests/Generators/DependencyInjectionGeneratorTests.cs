using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PureES.Core.Generators;
using PureES.Core.Generators.Models;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Core.Tests.Generators;

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
        var aggregateTypes = typeof(TestAggregates)
            .GetNestedTypes()
            .Where(t => t.GetCustomAttribute(typeof(AggregateAttribute)) != null);

        var aggregates = new List<Aggregate>();

        foreach (var t in aggregateTypes)
        {
            var log = new FakeErrorLog();
            var built = PureESTreeBuilder.BuildAggregate(new ReflectedType(t), out var aggregate, log);
            log.Errors.ShouldBeEmpty();
            built.ShouldBeTrue();
            aggregates.Add(aggregate);
        }

        aggregates.ShouldNotBeEmpty();

        var methods = typeof(TestAggregates)
            .GetNestedTypes()
            .SelectMany(t => new ReflectedType(t).Methods)
            .Where(m => m.HasAttribute<EventHandlerAttribute>());
        var eventHandlers = new List<EventHandler>();
        foreach (var m in methods)
        {
            var log = new FakeErrorLog();
            var built = PureESTreeBuilder.BuildEventHandler(m, out var handler, log);
            log.Errors.ShouldBeEmpty();
            built.ShouldBeTrue();
            eventHandlers.Add(handler);
        }

        eventHandlers.ShouldNotBeEmpty();

        var csharp = DependencyInjectionGenerator.Generate(aggregates, 
            EventHandlerCollection.Create(eventHandlers), 
            out var filename);
        filename.ShouldNotBeNullOrWhiteSpace();
        csharp.ShouldNotBeNullOrEmpty();
        _output.WriteLine(csharp);
    }
}