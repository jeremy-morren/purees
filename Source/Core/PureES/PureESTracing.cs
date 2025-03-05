using System.Diagnostics;

namespace PureES;

public static class PureESTracing
{
    /// <summary>
    /// PureES activity source
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new("PureES");
}