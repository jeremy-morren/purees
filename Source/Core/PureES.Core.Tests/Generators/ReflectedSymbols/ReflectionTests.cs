﻿using System;
using PureES.Core.Tests.Models;
using Shouldly;
using Xunit;

namespace PureES.Core.Tests.Generators.ReflectedSymbols;

public class ReflectionTests
{
    [Theory]
    [InlineData(typeof(TestAggregate))]
    public void GetReflectedType(Type type)
    {
        var t = new ReflectedType(type);
        t.Constructors.ShouldNotBeEmpty();
        t.Attributes.ShouldNotBeEmpty();
        t.Properties.ShouldNotBeEmpty();
        t.Methods.ShouldNotBeEmpty();
    }
}