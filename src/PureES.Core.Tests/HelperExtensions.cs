using System.Collections.Generic;
using System.Linq;

namespace PureES.Core.Tests;

public static class HelperExtensions
{
    public static bool Contains<T>(this IEnumerable<object> source) 
        where T : notnull => source.Any(s => s is T);
}