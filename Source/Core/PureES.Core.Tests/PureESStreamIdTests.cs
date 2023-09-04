using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PureES.Core.Tests;

public class PureESStreamIdTests
{
    [Fact]
    public void FromString()
    {
        var id = Guid.NewGuid().ToString();

        var svc = new PureESStreamId<string>(new PureESOptions());

        svc.GetId(id).ShouldBe(id);
    }
    
    [Fact]
    public void FromAggregateId()
    {
        var id = Guid.NewGuid().ToString();

        var svc = new PureESStreamId<AggregateId>(new PureESOptions());

        svc.GetId(new AggregateId(id)).ShouldBe(id);

        Assert.Throws<NullReferenceException>(() => svc.GetId(null!));
    }
    
    [Fact]
    public void FromCommand()
    {
        var options = new PureESOptions()
        {
            GetStreamIdProperty = t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .First(p => p.Name is "AggregateId" or "StreamId")
        };

        var svc = new PureESStreamId<Command>(options);
        var id = Guid.NewGuid().ToString();

        svc.GetId(new Command(new AggregateId(id))).ShouldBe(id);

        Assert.Throws<NullReferenceException>(() => svc.GetId(null!));
    }
    
    private record AggregateId(string StreamId);

    private record Command(AggregateId AggregateId);
}