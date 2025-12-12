using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable UnusedTypeParameter

namespace PureES.Tests;

public class BasicEventTypeMapTests
{
    private readonly ITestOutputHelper _output;

    public BasicEventTypeMapTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Dictionary<string, object?>))]
    [InlineData(typeof(Dictionary<string, BasicEventTypeMap?>))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(BasicEventTypeMapTests))]
    [InlineData(typeof(IAggregateStore<List<string>>))]
    [InlineData(typeof(NestedType))]
    [InlineData(typeof(SubType))]
    public void MapShouldReturnSameType(Type type)
    {
        var list = BasicEventTypeMap.GetTypeNames(type);
        list.Should().NotBeEmpty()
            .And.OnlyHaveUniqueItems()
            .And.NotContain(t => t.StartsWith("System.Object"), "should not include System.Object")
            .And.NotContain(t => t.StartsWith("System.ValueType"), "should not include System.ValueType")
            .And.NotContain(t => t.Contains("Version="), "Should not include assembly version")
            .And.NotContain(t => t.Contains(typeof(string).Assembly.GetName().Name!), "Should not include assembly name for System types")
            .And.NotContain(t => t.Contains("Equatable"), "Should not include IEquatable")
            .And.NotContain(t => t.Contains("Parsable"), "Should not include IParsable")
            .And.NotContain(t => t.Contains("Formattable"), "Should not include IFormattable")
            .And.NotContain(t => t.Contains("Comparable"), "Should not include IComparable")
            .And.NotContain(t => t.Contains("Serializable"), "Should not include Serializable")
            .And.NotContain(t => t.Contains("serialization", StringComparison.OrdinalIgnoreCase), "Should not include serialization interfaces")
            .And.NotContain(t => t.Contains("ICloneable"), "Should not include ICloneable")

            .And.OnlyContain(t => type.IsAssignableTo(BasicEventTypeMap.GetCLRType(t)), "All types should be assignable to the original type");

        _output.WriteLine(string.Join(Environment.NewLine, list));

        var mapped = BasicEventTypeMap.GetCLRType(list[^1]);
        mapped.ShouldBe(type);
    }

    [Theory]
    [InlineData("System.Collections.Generic.List`1[System.String]")]
    [InlineData("System.Collections.Generic.List`1[[PureES.PureESOptions, PureES]]")]
    [InlineData("System.Collections.Generic.Dictionary`2[System.String, System.Object]")]
    [InlineData("PureES.Tests.BasicEventTypeMapTests, PureES.Tests")]
    [InlineData("PureES.Tests.BasicEventTypeMapTests+SubType, PureES.Tests")]
    public void GetTypeFromStringShouldNotBeNull(string name)
    {
        var type = BasicEventTypeMap.GetCLRType(name);
        type.ShouldNotBeNull();
        
        var names = BasicEventTypeMap.GetTypeNames(type);
        names.ShouldNotBeEmpty();
        names[^1].ShouldBe(name);
    }

    [Fact]
    public void ListShouldIncludeInterfaces()
    {
        BasicEventTypeMap.GetTypeNames(typeof(SubType))
            .Should().Contain("PureES.Tests.IBaseInterface, PureES.Tests")
            .And.Contain("PureES.Tests.BasicEventTypeMapTests+ISubInterface`1[System.Int32], PureES.Tests");

        BasicEventTypeMap.GetTypeNames(typeof(List<string>))
            .Should().Contain("System.Collections.Generic.IReadOnlyCollection`1[System.String]")
            .And.Contain("System.Collections.Generic.IReadOnlyList`1[System.String]")
            .And.Contain("System.Collections.Generic.ICollection`1[System.String]");

        BasicEventTypeMap.GetTypeNames(typeof(ObservableCollection<SubType>))
            .Should().Contain("System.Collections.IList")
            .And.Contain("System.Collections.Generic.IList`1[[PureES.Tests.BasicEventTypeMapTests+SubType, PureES.Tests]]")
            .And.Contain("System.Collections.Generic.ICollection`1[[PureES.Tests.BasicEventTypeMapTests+SubType, PureES.Tests]]");

        BasicEventTypeMap.GetTypeNames(typeof(int))
            .Should().NotContain(t => t == "System.ValueType", "Should not include System.ValueType")
            .And.NotContain(t => t.Contains("Equatable"), "Should not include IEquatable")
            .And.NotContain(t => t.Contains("Comparable"), "Should not include IComparable")
            .And.NotContain(t => t.Contains("Parsable"), "Should not include Parsable")
            .And.NotContain(t => t.Contains("Formattable"), "Should not include Formattable");
    }

    private interface ISubInterface<T>;

    private class NestedType : IBaseInterface;

    private class SubType : NestedType, ISubInterface<int>, IComparable<string>
    {
        public int CompareTo(string? other) => throw new NotImplementedException();
    }

}

public interface IBaseInterface;