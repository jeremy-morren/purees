using System;
using System.Linq;
using System.Reflection;
using PureES.Core.EventStore;
using Shouldly;
using Xunit;

namespace PureES.Core.Tests;

public class CommandStreamIdTests
{
    private static GetCommandStreamIdProperty DefaultGetStreamIdProperty =>
        CommandPropertyStreamId<object>.DefaultGetStreamIdProperty;
    
    [Fact]
    public void FromString()
    {
        var id = Guid.NewGuid().ToString();

        var svc = new CommandPropertyStreamId<string>(DefaultGetStreamIdProperty);

        svc.GetStreamId(id).ShouldBe(id);
    }
    
    [Fact]
    public void FromAggregateId()
    {
        var id = Guid.NewGuid().ToString();

        var svc = new CommandPropertyStreamId<AggregateId>(DefaultGetStreamIdProperty);

        svc.GetStreamId(new AggregateId(id)).ShouldBe(id);

        Assert.Throws<NullReferenceException>(() => svc.GetStreamId(null!));
    }
    
    [Fact]
    public void FromCommand()
    {
        var svc = new CommandPropertyStreamId<Command>(t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .First(p => p.Name is "AggregateId" or "StreamId"));
        
        var id = Guid.NewGuid().ToString();

        svc.GetStreamId(new Command(new AggregateId(id))).ShouldBe(id);

        Assert.Throws<NullReferenceException>(() => svc.GetStreamId(null!));
    }
    
    private record AggregateId(string StreamId);

    private record Command(AggregateId AggregateId);
}