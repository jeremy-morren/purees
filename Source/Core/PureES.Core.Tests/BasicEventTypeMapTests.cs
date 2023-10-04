using System;
using System.Collections.Generic;
using PureES.Core.EventStore;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace PureES.Core.Tests;

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
    public void MapShouldReturnSameType(Type type)
    {
        var str = BasicEventTypeMap.GetTypeName(type);
        
        str.ShouldNotContain("Version=");
        str.ShouldNotContain(typeof(string).Assembly.GetName().Name!);

        _output.WriteLine(str);

        var mapped = BasicEventTypeMap.GetCLRType(str);
        mapped.ShouldBe(type);
    }

    [Theory]
    [InlineData("System.Collections.Generic.List`1[System.String]")]
    [InlineData("System.Collections.Generic.List`1[[PureES.Core.PureESOptions, PureES.Core]]")]
    [InlineData("System.Collections.Generic.Dictionary`2[System.String, System.Object]")]
    [InlineData("PureES.Core.Tests.BasicEventTypeMapTests, PureES.Core.Tests")]
    public void GetTypeFromStringShouldNotBeNull(string name)
    {
        var type = BasicEventTypeMap.GetCLRType(name);
        type.ShouldNotBeNull();
        
        BasicEventTypeMap.GetTypeName(type).ShouldBe(name);
    }
    
    private static class NestedType {}
}