namespace PureES.EventStore.Marten.CustomMethods;

internal static class MartenQueryableExtensions
{
    /// <summary>
    /// Matches whether the source enumerable intersects with the other enumerable
    /// </summary>
    /// <param name="enumerable"></param>
    /// <param name="other"></param>
    /// <returns></returns>
    /// <remarks>
    /// This method uses the <c>?|</c> operator in PostgreSQL
    /// </remarks>
    public static bool Intersects(this IEnumerable<string> enumerable, IEnumerable<string> other)
    {
        throw new NotImplementedException();
    }
}