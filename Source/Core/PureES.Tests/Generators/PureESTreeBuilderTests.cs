﻿using System;
using PureES.SourceGenerators;
using PureES.Tests.Framework;
using PureES.Tests.Generators.ReflectedSymbols;
using PureES.Tests.Models;
using Shouldly;
using Xunit;

namespace PureES.Tests.Generators;

public class PureESTreeBuilderTests
{
    [Theory]
    [InlineData(typeof(TestAggregate))]
    public void BuildAggregate(Type aggregate)
    {
        var log = new FakeErrorLog();

        var success = AggregateBuilder.BuildAggregate(new ReflectedType(aggregate, true), out var tree, log);

        log.Errors.ShouldBeEmpty();
        success.ShouldBeTrue();

        tree.ShouldNotBeNull();
        tree.Type.ShouldNotBeNull();
        tree.Handlers.ShouldNotBeEmpty();
        tree.When.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(typeof(TestEventHandlers))]
    [InlineData(typeof(ImplementedGenericEventHandlers))]
    public void BuildEventHandler(Type parentType)
    {
        var parent = new ReflectedType(parentType);
        var log = new FakeErrorLog();
        var success = EventHandlersBuilder.BuildEventHandlers(parent, out var handlers, log);
        log.Errors.ShouldBeEmpty();
        success.ShouldBeTrue();

        handlers.ShouldNotBeEmpty();
        Assert.All(handlers, h =>
        {
            h.Parent.ShouldBe(parent);
            h.Method.ShouldNotBeNull();
        });
    }
}