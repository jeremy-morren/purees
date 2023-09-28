using System;
using System.Linq;
using PureES.Core.Generators;
using PureES.Core.Tests.Framework;
using PureES.Core.Tests.Generators.ReflectedSymbols;
using PureES.Core.Tests.Models;
using Shouldly;
using Xunit;

namespace PureES.Core.Tests.Generators;

public class PureESTreeBuilderTests
{
    [Theory]
    [InlineData(typeof(TestAggregate))]
    public void BuildAggregate(Type aggregate)
    {
        var log = new FakeErrorLog();

        var success = PureESTreeBuilder.BuildAggregate(new ReflectedType(aggregate, true), out var tree, log);

        log.Errors.ShouldBeEmpty();
        success.ShouldBeTrue();

        tree.ShouldNotBeNull();
        tree.Type.ShouldNotBeNull();
        tree.Handlers.ShouldNotBeEmpty();
        tree.When.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(typeof(TestEventHandlers), nameof(TestEventHandlers.OnCreated))]
    [InlineData(typeof(TestEventHandlers), nameof(TestEventHandlers.OnUpdated))]
    [InlineData(typeof(TestEventHandlers), nameof(TestEventHandlers.OnCreated2))]
    public void BuildEventHandler(Type parent, string methodName)
    {
        var method = new ReflectedType(parent).Methods.Single(m => m.Name == methodName);

        var log = new FakeErrorLog();
        var success = PureESTreeBuilder.BuildEventHandler(method, out var handler, log);
        log.Errors.ShouldBeEmpty();
        success.ShouldBeTrue();

        handler.Method.ShouldBe(method);
        handler.Parent.ShouldBe(method.DeclaringType);
    }
}