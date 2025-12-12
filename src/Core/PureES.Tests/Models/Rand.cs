using System;

namespace PureES.Tests.Models;

public static class Rand
{
    public static int NextInt() => Random.Shared.Next();
    public static uint Nextuint() => (uint) Random.Shared.NextInt64();
}