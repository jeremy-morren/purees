using System;
using System.Linq.Expressions;
using PureES.Core.ExpBuilders;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders;

public class GetStreamIdBuilderTests
{
    [Fact]
    public void Default_Properties()
    {
        var cmd = Command.New();
        var exp = new GetStreamIdExpBuilder(new CommandHandlerBuilderOptions())
            .GetStreamId(Expression.Constant(cmd));
        var func = Expression.Lambda<Func<string>>(exp).Compile();
        Assert.Equal(cmd.Id.StreamId, func());
        Assert.NotEqual(cmd.Id.OtherStream, func());
        Assert.NotEqual(cmd.OtherId.StreamId, func());
        Assert.NotEqual(cmd.OtherId.OtherStream, func());
    }

    [Fact]
    public void Custom_AggregateId()
    {
        var builder = new GetStreamIdExpBuilder(new CommandHandlerBuilderOptions()
        {
            GetAggregateIdProperty = t =>
                t == typeof(Command) ? t.GetProperty(nameof(Command.OtherId)) : null,
        });
        var cmd = Command.New();
        var exp = builder.GetStreamId(Expression.Constant(cmd));
        var func = Expression.Lambda<Func<string>>(exp).Compile();
        Assert.NotEqual(cmd.Id.StreamId, func());
        Assert.NotEqual(cmd.Id.OtherStream, func());
        Assert.Equal(cmd.OtherId.StreamId, func());
        Assert.NotEqual(cmd.OtherId.OtherStream, func());
    }
    
    [Fact]
    public void Custom_StreamId()
    {
        var builder = new GetStreamIdExpBuilder(new CommandHandlerBuilderOptions()
        {
            GetStreamIdProperty = t =>
                t == typeof(AggId) ? t.GetProperty(nameof(AggId.OtherStream)) : null
        });
        var cmd = Command.New();
        var exp = builder.GetStreamId(Expression.Constant(cmd));
        var func = Expression.Lambda<Func<string>>(exp).Compile();
        Assert.NotEqual(cmd.Id.StreamId, func());
        Assert.Equal(cmd.Id.OtherStream, func());
        Assert.NotEqual(cmd.OtherId.StreamId, func());
        Assert.NotEqual(cmd.OtherId.OtherStream, func());
    }

    [Fact]
    public void Custom_AggregateId_And_StreamId()
    {
        
        var builder = new GetStreamIdExpBuilder(new CommandHandlerBuilderOptions()
        {
            GetAggregateIdProperty = t =>
                t == typeof(Command) ? t.GetProperty(nameof(Command.OtherId)) : null,
            GetStreamIdProperty = t =>
                t == typeof(AggId) ? t.GetProperty(nameof(AggId.OtherStream)) : null
        });
        var cmd = Command.New();
        var exp = builder.GetStreamId(Expression.Constant(cmd));
        var func = Expression.Lambda<Func<string>>(exp).Compile();
        Assert.NotEqual(cmd.Id.StreamId, func());
        Assert.NotEqual(cmd.Id.OtherStream, func());
        Assert.NotEqual(cmd.OtherId.StreamId, func());
        Assert.Equal(cmd.OtherId.OtherStream, func());
    }

    private record Command(AggId Id, AggId OtherId)
    {
        public static Command New() => new(AggId.New(), AggId.New());
    }

    private record AggId(Guid Id)
    {
        public string StreamId => $"Test-{Id}";
        public string OtherStream => $"Other-{Id}";
        public static AggId New() => new(Guid.NewGuid());
    }
}