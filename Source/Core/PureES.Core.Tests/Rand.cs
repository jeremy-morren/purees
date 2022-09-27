using System;

namespace PureES.Core.Tests;

public static class Rand
{
    public static int NextInt() => Random.Shared.Next();
    public static ulong NextULong() => (ulong)Random.Shared.NextInt64();
}