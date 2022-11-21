using System;
using PureES.Core.ExpBuilders.AggregateCmdHandlers;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders.AggregateCmdHandlers;

public class HandlerHelperTests
{
    [Theory]
    [InlineData(typeof(CommandResult<object, int>), true, typeof(object), typeof(int))]
    [InlineData(typeof(double), false, null, null)]
    [InlineData(typeof(OtherResult), true, typeof(int), typeof(string))]
    [InlineData(typeof(OtherResult<decimal>), true, typeof(decimal), typeof(string))]
    public void IsCommandResult(Type type, bool isCommandResult, Type? eventType, Type? resultType)
    {
        if (isCommandResult)
        {
            Assert.True(HandlerHelpers.IsCommandResult(type, out var @event, out var result));
            Assert.Equal(eventType, @event);
            Assert.Equal(resultType, result);
        }
        else
        {
            Assert.False(HandlerHelpers.IsCommandResult(type, out _, out _));
        }
    }

    private record OtherResult(int Event, string Result)
        : OtherResult<int>(Event, Result)
    {
    }

    private record OtherResult<TEvent>(TEvent Event, string Result)
        : CommandResult<TEvent, string>(Event, Result)
    {
    }
}