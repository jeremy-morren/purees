using System;

namespace PureES.Core.Tests.Models;

public static class Events
{
    public record Created(TestAggregateId Id, int Value)
    {
        public bool Equals(Commands.Create cmd) => cmd.Id == Id && cmd.Value == Value;
        public static Lazy<object> New() => new(() => new Created(TestAggregateId.New(), Rand.NextInt()), true);
    }

    public record Updated(TestAggregateId Id, int Value)
    {
        public bool Equals(Commands.Update cmd) => cmd.Id == Id && cmd.Value == Value;
    }
}