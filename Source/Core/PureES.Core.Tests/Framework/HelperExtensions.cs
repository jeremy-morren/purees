using System.Collections.Generic;
using System.Linq;

namespace PureES.Core.Tests.Framework;

public static class HelperExtensions
{
    public static bool Contains<T>(this IEnumerable<object> source)
        where T : notnull => source.Any(s => s is T);
}