﻿namespace PureES.Core.Tests.Models;

public static class Commands
{
    public record Create(TestAggregateId Id, int Value)
    {
        public static Create New() => new (TestAggregateId.New(), Rand.NextInt());
    }

    public record Update(TestAggregateId Id, int Value)
    {
        public static Update New() => new (TestAggregateId.New(), Rand.NextInt());
        public static Update New(TestAggregateId id) => new (id, Rand.NextInt());
    }
}