using System;

namespace PureES.Tests.Models;

public static class Rand
{
    public static int NextInt() => Random.Shared.Next();
    public static ulong NextULong() => (ulong) Random.Shared.NextInt64();
}