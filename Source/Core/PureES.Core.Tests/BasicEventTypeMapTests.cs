using System;
using System.Collections.Generic;
using PureES.Core.EventStore;
using Shouldly;
using Xunit;

namespace PureES.Core.Tests;

public class BasicEventTypeMapTests
{
    [Theory]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Dictionary<string, object?>))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(BasicEventTypeMapTests))]
    public void MapShouldReturnSameType(Type type)
    {
        var map = new BasicEventTypeMap();
        var str = map.GetTypeName(type);
        
        str.ShouldNotContain("Version=");
        str.ShouldNotContain("System.Private.CoreLib");
        str.ShouldNotContain("mscorlib");

        var mapped = map.GetCLRType(str);
        mapped.ShouldBe(type);
    }

    [Theory]
    [InlineData("System.Collections.Generic.List`1[System.String]")]
    [InlineData("System.Collections.Generic.List`1[[PureES.Core.PureESOptions, PureES.Core]]")]
    [InlineData("System.Collections.Generic.Dictionary`2[System.String, System.Object]")]
    [InlineData("PureES.Core.Tests.BasicEventTypeMapTests, PureES.Core.Tests")]
    public void GetTypeFromStringShouldNotBeNull(string name)
    {
        var map = new BasicEventTypeMap();
        var type = map.GetCLRType(name);
        type.ShouldNotBeNull();
        
        map.GetTypeName(type).ShouldBe(name);
    }
}