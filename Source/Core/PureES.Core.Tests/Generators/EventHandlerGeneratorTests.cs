using System;
using PureES.Core.SourceGenerators.Generators;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Core.Tests.Generators;

public class EventHandlerGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public EventHandlerGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(TestEventHandlers))]
    [InlineData(typeof(ImplementedGenericEventHandlers))]
    public void GenerateEventHandlers(Type parentType)
    {
        var parent = new ReflectedType(parentType);

        var log = new FakeErrorLog();
        var success = EventHandlersBuilder.BuildEventHandlers(parent, out var handlers, log);
        log.Errors.ShouldBeEmpty();
        success.ShouldBeTrue();

        handlers.ShouldNotBeEmpty();
        
        Assert.All(handlers, handler =>
        {
            var csharp = EventHandlerGenerator.Generate(handler, out var filename);
            filename.ShouldNotBeNullOrWhiteSpace();
            csharp.ShouldNotBeNullOrEmpty();
            _output.WriteLine(csharp);
        });
    }
}