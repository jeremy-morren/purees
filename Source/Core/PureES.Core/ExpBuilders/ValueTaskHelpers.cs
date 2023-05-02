namespace PureES.Core.ExpBuilders;

internal static class ValueTaskHelpers
{
    public static readonly MethodInfo FromTaskMethod = typeof(ValueTaskHelpers).GetStaticMethod(nameof(FromTask));
    
    public static readonly MethodInfo ToTaskVoidMethod = typeof(ValueTaskHelpers).GetStaticMethod(nameof(ToTaskVoid));

    public static readonly MethodInfo FromResultMethod = 
        typeof(ValueTask).GetStaticMethod(nameof(ValueTask.FromResult));

    public static readonly MethodInfo DefaultMethod = typeof(ValueTaskHelpers).GetStaticMethod(nameof(Default));

    public static ValueTask<T?> Default<T>() => ValueTask.FromResult<T?>(default);

    public static async ValueTask<T> FromTask<T>(Task<T> source) => await source;

    public static async Task ToTaskVoid(ValueTask source) => await source;
}