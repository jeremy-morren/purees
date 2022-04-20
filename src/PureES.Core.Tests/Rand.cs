using System;

namespace PureES.Core.Tests;

public static class Rand
{
    /// <summary>
    /// Singleton instance of <see cref="System.Random"/>
    /// </summary>
    /// <remarks>Avoid creating in most tests</remarks>
    private static readonly System.Random Source = new ();

    public static int NextInt() => Source.Next();
    public static ulong NextULong() => (ulong)Source.NextInt64();
}