using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Tests;

public class BasicEventTypeMapTests
{
    private readonly ITestOutputHelper _output;

    public BasicEventTypeMapTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
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
        list.ShouldNotBeEmpty();
        list.ShouldNotContain(s => s.StartsWith("System.Object"));
        list.ShouldNotContain(s => s.Contains("Version="));
        list.ShouldNotContain(s => s.Contains(typeof(string).Assembly.GetName().Name!));
        
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
    
    private class NestedType {}
    private class SubType : NestedType {}
}