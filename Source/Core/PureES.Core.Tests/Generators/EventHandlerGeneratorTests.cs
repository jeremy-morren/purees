using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PureES.Core.Generators;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using EventHandler = PureES.Core.Generators.Models.EventHandler;

namespace PureES.Core.Tests.Generators;

public class EventHandlerGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public EventHandlerGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    // [InlineData(typeof(TestEventHandlers), typeof(Events.Created))]
    // [InlineData(typeof(TestEventHandlers), typeof(Events.Updated))]
    [InlineData(typeof(ImplementedGenericEventHandlers), typeof(Events.Created))]
    public void GenerateEventHandlers(Type handlerType, Type eventType)
    {
        var @event = new ReflectedType(eventType);

        var methods = new ReflectedType(handlerType)
            .GetMethodsRecursive()
            .Where(m => m.HasAttribute<EventHandlerAttribute>());

        var handlers = new List<EventHandler>();
        foreach (var m in methods)
        {
            var log = new FakeErrorLog();
            var success = PureESTreeBuilder.BuildEventHandler(m, out var handler, log);
            log.Errors.ShouldBeEmpty();
            success.ShouldBeTrue();
            if (handler.Event.Equals(@event))
                handlers.Add(handler);
        }

        handlers.ShouldNotBeEmpty();

        var collection = new EventHandlerCollection(@event, handlers);

        var csharp = EventHandlerGenerator.Generate(collection, out var filename);
        filename.ShouldNotBeNullOrWhiteSpace();
        csharp.ShouldNotBeNullOrEmpty();
        _output.WriteLine(csharp);
    }
}