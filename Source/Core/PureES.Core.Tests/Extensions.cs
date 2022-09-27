using System;
using System.Collections.Generic;

namespace PureES.Core.Tests;

public static class Extensions
{
#pragma warning disable CS1998
    public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        foreach (var s in source)
            yield return s;
    }
#pragma warning restore CS1998
}